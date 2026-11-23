// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ActualizarCentro;

public static class ActualizarCentroEndpoint
{
    public static void MapActualizarCentro(this IEndpointRouteBuilder endpoints)
    {
        CenterValidation(endpoints.MapPatch("/{id:guid}", async (Guid id, CenterRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            await RequireManagement(db, actor, clock, id, ct);
            var c = await db.Centers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            var now = clock.GetUtcNow();
            if (!r.Activo && await HasCommitments(db, id, null, now, ct))
            {
                throw ApiContract.Conflict("CENTRO_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de desactivar el centro.");
            }

            c.Name = r.Nombre.Trim();
            c.Address = r.Direccion.Trim();
            if (c.Active != r.Activo)
            {
                c.Active = r.Activo;
                QueueAvailability(db, id, now);
            }

            return ApiReply.Ok(Response(c));
        })).Document<CenterResponse>("ActualizarCentro", "PA-12b,c"));
    }
}
