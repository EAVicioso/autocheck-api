// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Permisos.ListarPermisos;

public static class ListarPermisosEndpoint
{
    public static void MapListarPermisos(this IEndpointRouteBuilder routes) => routes.MapGet("/api/permisos", HandleAsync).RequireAuthorization("GestionUsuarios").WithTags("Permisos").Document<ItemsResponse<Response>>("ListarPermisos", "CU-11 / PA-11");
    private static Results<Ok<ItemsResponse<Response>>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult> HandleAsync(Handler handler) => TypedResults.Ok(handler.Execute());
}
