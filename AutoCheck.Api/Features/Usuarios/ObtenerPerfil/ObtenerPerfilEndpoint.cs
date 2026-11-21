using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

public static class ObtenerPerfilEndpoint
{
    public static void MapObtenerPerfil(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/me", HandleAsync).RequireAuthorization("SesionActiva").Document<UserResponse>("ObtenerPerfil", "CU-11 / PA-11");
    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(ct));
}
