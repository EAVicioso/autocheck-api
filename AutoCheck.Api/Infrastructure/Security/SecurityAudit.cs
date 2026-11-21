using System.Globalization;
namespace AutoCheck.Api.Infrastructure.Security;
// Independent scope: denied transactions never erase security evidence.
public sealed class SecurityAudit(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SecurityAudit> logger)
{
    public async Task Record(HttpContext http, int status, AppActor actor)
    {
        if (http.Items.ContainsKey(typeof(SecurityAudit)))
        {
            return;
        }
        http.Items[typeof(SecurityAudit)] = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
            db.Audit.Add(new() { ActorId = actor.Id == Guid.Empty ? null : actor.Id, At = clock.GetUtcNow(), Operation = "SEGURIDAD_RECHAZADA", Entity = http.Request.Path, Outcome = status.ToString(CultureInfo.InvariantCulture), Correlation = http.TraceIdentifier, Ip = http.Connection.RemoteIpAddress });
            await db.SaveChangesAsync(deadline.Token);
        }
        catch (Exception error) when (error is Npgsql.NpgsqlException or DbUpdateException or OperationCanceledException)
        {
            SecurityAuditLog.Unavailable(logger, http.TraceIdentifier);
        }
    }
}
public sealed class SecurityAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, SecurityAudit audit, AppActor actor)
    {
        try
        {
            await next(http);
        }
        catch (ApiException error) when (error.Status is 401 or 403 or 429 || error.Code == "INTEGRIDAD_NO_VALIDA")
        {
            await audit.Record(http, error.Status, actor);
            throw;
        }
        if (http.Response.StatusCode is 403 or 429 || (http.Response.StatusCode == 401 && http.Request.Headers.ContainsKey("Authorization")))
        {
            await audit.Record(http, http.Response.StatusCode, actor);
        }
    }
}
public static partial class SecurityAuditLog
{
    [LoggerMessage(Level = LogLevel.Critical, Message = "Security audit persistence unavailable; correlation {Correlation}")]
    public static partial void Unavailable(ILogger logger, string correlation);
}
