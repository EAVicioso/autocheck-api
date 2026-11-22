// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Permisos.ObtenerAcceso;

public static class ObtenerAccesoEndpoint
{
    public static void MapObtenerAcceso(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}/acceso", HandleAsync).RequireAuthorization("GestionUsuarios").Document<AccessResponse>("ObtenerAcceso", "CU-11 / PA-11");
    private static async Task<Results<Ok<AccessResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
