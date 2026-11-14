using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public static class ApiContract
{
    public static int Page(HttpContext context)
    {
        if (!int.TryParse(context.Request.Query["pagina"].FirstOrDefault() ?? "1", out var value) || value < 1 || value > 500000)
        {
            throw Invalid("Página inválida.");
        }

        return value;
    }

    public static int Size(HttpContext context)
    {
        if (!int.TryParse(context.Request.Query["tamano"].FirstOrDefault() ?? "20", out var value) || value is < 1 or > 20)
        {
            throw Invalid("El tamaño debe estar entre 1 y 20.");
        }

        return value;
    }

    public static (DateTimeOffset? From, DateTimeOffset? To) UtcRange(DateOnly? from, DateOnly? to)
    {
        if (from > to || to == DateOnly.MaxValue)
        {
            throw Invalid("Rango de fechas inválido.");
        }

        return (from is null ? null : DominicanTime.Start(from.Value), to is null ? null : DominicanTime.Start(to.Value.AddDays(1)));
    }

    public static void Filter(string? value, params string[] allowed)
    {
        if (value is not null && !allowed.Contains(value))
        {
            throw Invalid("Filtro inválido.");
        }
    }

    public static ApiException Invalid(string message) => new(400, "SOLICITUD_INVALIDA", message);
    public static ApiException Missing() => new(404, "RECURSO_NO_ENCONTRADO", "Recurso no encontrado.");
    public static ApiException Conflict(string code, string message) => new(409, code, message);
    public static ApiException Unavailable() => new(503, "SERVICIO_NO_DISPONIBLE", "Servicio temporalmente no disponible.");
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    extension(RouteHandlerBuilder route)
    {
        public RouteHandlerBuilder Document<T>(string name, string requirement, int status = 200) => route.WithName(name).WithSummary(requirement).Produces<T>(status).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        public RouteHandlerBuilder Validated<T>(Action<InlineValidator<T>> rules)
            where T : class
        {
            var validator = new InlineValidator<T>();
            rules(validator);
            return route.AddEndpointFilter(async (context, next) =>
            {
                var request = context.Arguments.OfType<T>().Single();
                var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    return Results.ValidationProblem(result.ToDictionary(), extensions: new Dictionary<string, object?> { ["codigo"] = "SOLICITUD_INVALIDA", ["traceId"] = context.HttpContext.TraceIdentifier });
                }

                return await next(context);
            });
        }
    }
}
