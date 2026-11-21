using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Permisos.ListarRoles;

public static class ListarRolesEndpoint
{
    public static void MapListarRoles(this IEndpointRouteBuilder routes) => routes.MapGet("/api/roles", HandleAsync).RequireAuthorization("GestionUsuarios").WithTags("Permisos").Document<ItemsResponse<Response>>("ListarRoles", "CU-11 / PA-11");
    private static Results<Ok<ItemsResponse<Response>>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult> HandleAsync(Handler handler) => TypedResults.Ok(handler.Execute());
}
