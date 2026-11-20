using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public static class ListarUsuariosEndpoint
{
    public static void MapListarUsuarios(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("", HandleAsync).RequireAuthorization().WithTags("Usuarios").Document<PageResponse<UserResponse>>("ListarUsuarios", "CU-11 / PA-11");
    private static async Task<Results<Ok<PageResponse<UserResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
