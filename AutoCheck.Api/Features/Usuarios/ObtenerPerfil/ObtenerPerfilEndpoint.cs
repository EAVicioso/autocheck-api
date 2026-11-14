using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

public static class ObtenerPerfilEndpoint
{
    public static void MapObtenerPerfil(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me", async (AutoCheckDb db, AppActor actor, CancellationToken ct) => Response(await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor.Id, ct))).Document<UserResponse>("ObtenerPerfil", "6.2.1.1 / CU-11");
    }
}
