using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ActualizarCentro;

public static class ActualizarCentroEndpoint
{
    public static void MapActualizarCentro(this IEndpointRouteBuilder endpoints)
    {
        CenterValidation(endpoints.MapPatch("/{id:guid}", async (Guid id, CenterRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            actor.RequireCenter(id);
            await db.LockAsync("center:" + id, ct);
            var c = await db.Centers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!r.Activo && await HasCommitments(db, id, null, clock.GetUtcNow(), ct))
            {
                throw ApiContract.Conflict("CENTRO_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de desactivar el centro.");
            }

            c.Name = r.Nombre.Trim();
            c.Address = r.Direccion.Trim();
            c.Active = r.Activo;
            return ApiReply.Ok(Response(c));
        })).Document<CenterResponse>("ActualizarCentro", "PA-12b,c"));
    }
}
