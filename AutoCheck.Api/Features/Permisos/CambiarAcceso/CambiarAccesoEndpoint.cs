// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Permisos.CambiarAcceso;

public static class CambiarAccesoEndpoint
{
    public static void MapCambiarAcceso(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/{id:guid}/acceso", HandleAsync).RequireAuthorization("GestionUsuarios").Interactive().WithMetadata(new MutationAudit("CAMBIO_ACCESO", UseUserId: true)).Document<UserResponse>("CambiarAcceso", "CU-11 / PA-11a,b").Validated<AccessRequest>();
    }

    private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, AccessRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(id, r, h, ct);
        return TypedResults.Ok(reply.Body!);
    }
}
