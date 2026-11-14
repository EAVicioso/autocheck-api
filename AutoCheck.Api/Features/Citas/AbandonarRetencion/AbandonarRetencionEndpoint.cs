using AutoCheck.Api.Features.Citas.Common;
using static AutoCheck.Api.Features.Citas.Common.AppointmentSupport;

namespace AutoCheck.Api.Features.Citas.AbandonarRetencion;

public static class AbandonarRetencionEndpoint
{
    public static void MapAbandonarRetencion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/retenciones/{id:guid}", async (Guid id, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, new { id }, async ct =>
        {
            var p = await db.Payments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (p.UserId != actor.Id)
            {
                AppActor.Deny();
            }

            await db.LockAsync("center:" + p.CenterId, ct);
            await db.Entry(p).ReloadAsync(ct);
            if (p.State == "APROBADO")
            {
                throw ApiContract.Conflict("CITA_CONFIRMADA", "Cancela la cita confirmada mediante su operación de cancelación.");
            }

            var b = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == p.RampId && x.Starts == p.Starts && x.AttemptId == id, ct);
            if (b?.State == "RETENIDO")
            {
                db.Blocks.Remove(b);
            }

            if (p.State == "PENDIENTE")
            {
                p.State = "ABANDONADO";
            }

            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = clock.GetUtcNow(), Payload = ApiReply.Serialize(new { centroId = p.CenterId }) });
            return ApiReply.Empty();
        })).Interactive().Document<object>("AbandonarRetencion", "RF-CIT-02 / CU-03", 204);
    }
}
