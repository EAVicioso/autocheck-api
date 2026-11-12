// AutoCheck academic prototype, SRS 1.1; IC-02, RS-03.
using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record ApiReply(int Status, string Json, string? Location = null) : IResult
{
    public static ApiReply Ok(object body) => new(200, Serialize(body));
    public static ApiReply Created(string location, object body) => new(201, Serialize(body), location);
    public static ApiReply Empty() => new(204, "");
    public static string Serialize(object body) => JsonSerializer.Serialize(body, JsonOptions);
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = Status;
        if (Location is not null) context.Response.Headers.Location = Location;
        if (Status != 204)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(Json, context.RequestAborted);
        }
    }
}

public static class ApiContract
{
    public static RouteHandlerBuilder Document<T>(this RouteHandlerBuilder route, string name, string requirement, int status = 200) =>
        route.WithName(name).WithSummary(requirement).Produces<T>(status)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);

    public static RouteHandlerBuilder Validated<T>(this RouteHandlerBuilder route, Action<InlineValidator<T>> rules) where T : class
    {
        var validator = new InlineValidator<T>();
        rules(validator);
        return route.AddEndpointFilter(async (context, next) =>
        {
            var request = context.Arguments.OfType<T>().Single();
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary(), extensions: new Dictionary<string, object?>
                { ["codigo"] = "SOLICITUD_INVALIDA", ["traceId"] = context.HttpContext.TraceIdentifier });
            return await next(context);
        });
    }

    public static int Page(HttpContext context)
    {
        if (!int.TryParse(context.Request.Query["pagina"].FirstOrDefault() ?? "1", out var value) || value < 1 || value > 500000)
            throw Invalid("Página inválida.");
        return value;
    }

    public static int Size(HttpContext context)
    {
        if (!int.TryParse(context.Request.Query["tamano"].FirstOrDefault() ?? "20", out var value) || value is < 1 or > 20)
            throw Invalid("El tamaño debe estar entre 1 y 20.");
        return value;
    }

    public static (DateTimeOffset? From, DateTimeOffset? To) UtcRange(DateOnly? from, DateOnly? to)
    {
        if (from > to || to == DateOnly.MaxValue) throw Invalid("Rango de fechas inválido.");
        return (from is null ? null : DominicanTime.Start(from.Value), to is null ? null : DominicanTime.Start(to.Value.AddDays(1)));
    }
    public static void Filter(string? value, params string[] allowed)
    {
        if (value is not null && !allowed.Contains(value)) throw Invalid("Filtro inválido.");
    }
    public static ApiException Invalid(string message) => new(400, "SOLICITUD_INVALIDA", message);
    public static ApiException Missing() => new(404, "RECURSO_NO_ENCONTRADO", "Recurso no encontrado.");
    public static ApiException Conflict(string code, string message) => new(409, code, message);
    public static ApiException Unavailable() => new(503, "SERVICIO_NO_DISPONIBLE", "Servicio temporalmente no disponible.");
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

public sealed class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            var error = ex switch
            {
                ApiException known => known,
                DbUpdateConcurrencyException => ApiContract.Conflict("VERSION_DESACTUALIZADA", "El registro cambió. Actualiza y reintenta."),
                DbUpdateException d when d.InnerException is Npgsql.PostgresException { SqlState: "23505" } => ApiContract.Conflict("REGISTRO_DUPLICADO", "El registro ya existe."),
                BadHttpRequestException => ApiContract.Invalid("JSON o solicitud inválidos."),
                HttpRequestException or TaskCanceledException or Npgsql.NpgsqlException => ApiContract.Unavailable(),
                _ => new ApiException(500, "ERROR_INTERNO", "No se pudo completar la operación.")
            };
            logger.LogWarning("Request {TraceId} failed with {Code} ({ExceptionType})", context.TraceIdentifier, error.Code, ex.GetType().Name);
            context.Response.Clear();
            if (context.Request.Path == "/api/asistencia")
            {
                await Results.Json(new { respuesta = error.Status == 503 ? AutoCheck.Api.Features.Chatbot.AssistanceEndpoints.Contingency : error.Message,
                    categoria = "NO_DETERMINADA", codigo = error.Status == 503 ? "SERVICIO_NO_DISPONIBLE" : "SOLICITUD_INVALIDA" }, statusCode: error.Status).ExecuteAsync(context);
                return;
            }
            await Results.Problem(statusCode: error.Status, title: error.Message, extensions: new Dictionary<string, object?>
            { ["codigo"] = error.Code, ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }
}

public static class DatabaseLocks
{
    // Transaction-scoped, shared across API replicas. Locks are never process-local.
    public static Task LockAsync(this AutoCheckDb db, string resource, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
}

public sealed class Mutation(AutoCheckDb db, AppActor actor, TimeProvider clock)
{
    public async Task<ApiReply> Run(HttpContext http, object request, Func<CancellationToken, Task<ApiReply>> action, bool idempotent = false)
    {
        var ct = http.RequestAborted;
        var key = http.Request.Headers["Idempotency-Key"].ToString();
        if (idempotent && (string.IsNullOrWhiteSpace(key) || key.Length > 128))
            throw ApiContract.Invalid("Idempotency-Key es obligatorio y admite hasta 128 caracteres.");
        var operation = $"{http.Request.Method}:{http.Request.Path}";
        var storageKey = ApiContract.Hash($"{actor.Id}:{operation}:{key}");
        var fingerprint = ApiContract.Hash(ApiReply.Serialize(request));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (idempotent)
        {
            await db.LockAsync("idempotency:" + storageKey, ct);
            var previous = await db.Idempotency.FindAsync([storageKey], ct);
            if (previous is not null)
            {
                if (previous.Fingerprint != fingerprint) throw ApiContract.Conflict("IDEMPOTENCIA_CONFLICTO", "La clave fue utilizada con otra solicitud.");
                return new(previous.Status, previous.Json, previous.Location);
            }
        }
        var reply = await action(ct);
        if (idempotent) db.Idempotency.Add(new() { Key = storageKey, Fingerprint = fingerprint, Status = reply.Status, Json = reply.Json, Location = reply.Location });
        db.Audit.Add(new() { ActorId = actor.Id == Guid.Empty ? null : actor.Id, At = clock.GetUtcNow(), Operation = http.GetEndpoint()?.DisplayName ?? operation,
            Entity = http.Request.Path, Outcome = "CONFIRMADO", Correlation = http.TraceIdentifier });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return reply;
    }
}

public static class DominicanTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
    public static DateOnly Date(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Zone).DateTime);
    public static DateTimeOffset Start(DateOnly day) => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone));
}
