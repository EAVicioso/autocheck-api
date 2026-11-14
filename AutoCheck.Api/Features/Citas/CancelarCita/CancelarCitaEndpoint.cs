using AutoCheck.Api.Features.Citas.Common;
using static AutoCheck.Api.Features.Citas.Common.AppointmentSupport;

namespace AutoCheck.Api.Features.Citas.CancelarCita;

public static class CancelarCitaEndpoint
{
    public static void MapCancelarCita(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/{id:guid}/cancelacion", async (Guid id, CancelRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (a.UserId != actor.Id)
            {
                AppActor.Deny();
            }

            await db.LockAsync("center:" + a.CenterId, ct);
            await db.Entry(a).ReloadAsync(ct);
            var payment = await db.Payments.SingleAsync(x => x.Id == a.AttemptId, ct);
            if (a.State == "CANCELADA")
            {
                return ApiReply.Ok(new { citaId = a.Id, estado = a.State, reversionId = payment.ReversalId });
            }

            if (a.State != "CONFIRMADA" || a.Starts <= clock.GetUtcNow() || await db.Inspections.AnyAsync(x => x.AppointmentId == id, ct))
            {
                throw ApiContract.Conflict("CITA_NO_CANCELABLE", "La cita ya no puede cancelarse.");
            }

            a.State = "CANCELADA";
            payment.State = "REVERTIDO";
            payment.ReversalId ??= Guid.NewGuid();
            var block = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == a.RampId && x.Starts == a.Starts && x.AttemptId == a.AttemptId, ct);
            if (block is not null)
            {
                db.Blocks.Remove(block);
            }

            db.Outbox.Add(new() { Kind = "CITA_CANCELADA", AggregateId = a.Id, Available = clock.GetUtcNow() });
            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = clock.GetUtcNow(), Payload = ApiReply.Serialize(new { centroId = a.CenterId }) });
            return ApiReply.Ok(new { citaId = a.Id, estado = a.State, reversionId = payment.ReversalId });
        }, true)).Interactive().Document<CancellationResponse>("CancelarCita", "RF-CIT-02 / PA-03e / PA-04d").Validated<CancelRequest>(v => v.RuleFor(x => x.Motivo).NotEmpty().MaximumLength(300));
    }
}

public sealed record CancellationResponse(Guid CitaId, string Estado, Guid? ReversionId);
public sealed record CancelRequest(string Motivo);
