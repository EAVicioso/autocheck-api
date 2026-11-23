// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Auth.RegistrarActividad;

public static class RegistrarActividadEndpoint
{
    public static void MapRegistrarActividad(this IEndpointRouteBuilder endpoints) => endpoints.MapPost("/actividad", HandleAsync).RequireAuthorization("SesionActiva").WithMetadata(new MutationAudit("ACTIVIDAD_REGISTRADA")).Document<object>("RegistrarActividad", "RS-10 / PA-02", 204);
    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Handler handler, CancellationToken ct)
    {
        await handler.Execute(ct);
        return TypedResults.NoContent();
    }
}
