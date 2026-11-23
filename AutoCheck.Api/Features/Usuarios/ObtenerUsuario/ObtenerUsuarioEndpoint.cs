// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerUsuario;

public static class ObtenerUsuarioEndpoint
{
    public static void MapObtenerUsuario(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}", HandleAsync).RequireAuthorization("GestionUsuarios").Document<UserResponse>("ObtenerUsuario", "CU-11 / PA-11");
    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
