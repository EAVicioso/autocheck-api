// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public static class RestablecerContrasenaEndpoint
{
    public static void MapRestablecerContrasena(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/restablecimiento", HandleAsync).AllowAnonymous().Document<object>("RestablecerContrasena", "RS-09 / RS-11", 204).Validated<ResetRequest>();
    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(ResetRequest request, RestablecerContrasenaHandler handler, HttpContext http, CancellationToken ct)
    {
        await handler.Execute(request, http.TraceIdentifier, ct);
        return TypedResults.NoContent();
    }
}
