using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.CerrarSesion;

public static class CerrarSesionEndpoint
{
    public static void MapCerrarSesion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/logout", async (HttpContext h, AppActor actor, AutoCheckDb db, CancellationToken ct) =>
        {
            // Local revocation is immediate even while the identity provider is unavailable.
            await db.Sessions.Where(x => x.Id == actor.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
            return Results.NoContent();
        }).RequireAuthorization().Document<object>("CerrarSesion", "CU-02 / RS-10", 204);
    }
}
