// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.CrearCentro;

public static class CrearCentroEndpoint
{
    public static void MapCrearCentro(this IEndpointRouteBuilder endpoints)
    {
        CenterValidation(endpoints.MapPost("", async (CenterRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            await db.LockAsync("user:" + actor.Id, ct);
            await actor.RevalidateAuthority(db, clock, ct);
            actor.RequirePermission(Permissions.Centers);
            if (actor.CenterId is not null)
            {
                AppActor.Deny();
            }

            var c = new Center
            {
                Name = r.Nombre.Trim(),
                Address = r.Direccion.Trim(),
                Active = r.Activo
            };
            db.Centers.Add(c);
            return ApiReply.Created($"/api/centros/{c.Id}", Response(c));
        })).Document<CenterResponse>("CrearCentro", "CU-12 / PA-12c", 201));
    }
}
