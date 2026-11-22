// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public static class ActualizarPerfilEndpoint
{
    public static void MapActualizarPerfil(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/me", HandleAsync).Interactive().WithMetadata(new MutationAudit("PERFIL_ACTUALIZADO")).Document<UserResponse>("ActualizarPerfil", "SRS § 6.2.1.1").Validated<ProfileRequest>();
    }
    private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(ProfileRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(r, h, ct);
        return TypedResults.Ok(reply.Body!);
    }
}
