using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ActualizarRampa;

public static class ActualizarRampaEndpoint
{
    public static void MapActualizarRampa(this IEndpointRouteBuilder endpoints)
    {
        RampValidation(endpoints.MapPatch("/{id:guid}/rampas/{rampaId:guid}", async (Guid id, Guid rampaId, RampRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            actor.RequireCenter(id);
            await db.LockAsync("center:" + id, ct);
            var ramp = await db.Ramps.SingleOrDefaultAsync(x => x.Id == rampaId && x.CenterId == id, ct) ?? throw ApiContract.Missing();
            if (!r.Activa && await HasCommitments(db, id, rampaId, clock.GetUtcNow(), ct))
            {
                throw ApiContract.Conflict("RAMPA_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de desactivar la rampa.");
            }

            ramp.Name = r.Nombre.Trim();
            ramp.Active = r.Activa;
            return ApiReply.Ok(new RampResponse(ramp.Id, id, ramp.Name, ramp.Active));
        })).Document<RampResponse>("ActualizarRampa", "PA-12b,c"));
    }
}
