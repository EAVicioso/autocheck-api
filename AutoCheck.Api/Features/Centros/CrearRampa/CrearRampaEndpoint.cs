using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.CrearRampa;

public static class CrearRampaEndpoint
{
    public static void MapCrearRampa(this IEndpointRouteBuilder endpoints)
    {
        RampValidation(endpoints.MapPost("/{id:guid}/rampas", async (Guid id, RampRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            actor.RequireCenter(id);
            await db.LockAsync("center:" + id, ct);
            if (!await db.Centers.AnyAsync(x => x.Id == id && x.Active, ct))
            {
                throw ApiContract.Missing();
            }

            var ramp = new Ramp
            {
                CenterId = id,
                Name = r.Nombre.Trim(),
                Active = r.Activa
            };
            db.Ramps.Add(ramp);
            return ApiReply.Created($"/api/centros/{id}", new RampResponse(ramp.Id, id, ramp.Name, ramp.Active));
        })).Document<RampResponse>("CrearRampa", "CU-12 / PA-12a", 201));
    }
}
