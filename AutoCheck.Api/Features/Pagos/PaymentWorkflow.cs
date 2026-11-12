// AutoCheck academic prototype, SRS 1.1; RF-CIT-01/02/03, CU-04, PA-03/04.
namespace AutoCheck.Api.Features.Pagos;

public sealed record SandboxEvent(string EventoId, Guid IntentoId, string Resultado);
public sealed record PaymentStateResponse(Guid IntentoId, Guid RetencionId, string Estado, DateTimeOffset ExpiraUtc, Guid? CitaId, Guid? ReversionId, decimal Monto, string Moneda);
public interface IPaymentSandbox { Task<SandboxEvent> Process(PaymentAttempt attempt, CancellationToken ct); }
public sealed class AcademicPaymentSandbox : IPaymentSandbox
{
    public Task<SandboxEvent> Process(PaymentAttempt attempt, CancellationToken ct) => Task.FromResult(new SandboxEvent("sandbox:" + attempt.Id, attempt.Id, attempt.Scenario));
}

public sealed class PaymentWorkflow(AutoCheckDb db, TimeProvider clock)
{
    public async Task<PaymentStateResponse> Apply(SandboxEvent e, string correlation, CancellationToken ct)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == e.IntentoId, ct) ?? throw ApiContract.Missing();
        await db.LockAsync("center:" + payment.CenterId, ct);
        await db.Entry(payment).ReloadAsync(ct);
        var previous = await db.PaymentEvents.FindAsync([e.EventoId], ct);
        if (previous is not null)
        {
            if (previous.AttemptId != e.IntentoId || previous.Outcome != e.Resultado) throw ApiContract.Conflict("EVENTO_CONFLICTO", "El identificador de evento ya fue utilizado.");
            return await State(payment, ct);
        }
        db.PaymentEvents.Add(new() { Id = e.EventoId, AttemptId = e.IntentoId, Outcome = e.Resultado });
        if (payment.State == "APROBADO" || payment.ReversalId is not null) return await State(payment, ct);
        var block = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == payment.RampId && x.Starts == payment.Starts, ct);
        var valid = block is not null && block.AttemptId == payment.Id && block.State == "RETENIDO" && block.Expires > clock.GetUtcNow() && payment.State == "PENDIENTE";
        if (e.Resultado == "APROBADO" && valid)
        {
            payment.State = "APROBADO"; block!.State = "CONFIRMADO";
            var appointment = new Appointment { AttemptId = payment.Id, UserId = payment.UserId, VehicleId = payment.VehicleId, CenterId = payment.CenterId, RampId = payment.RampId,
                Starts = payment.Starts, PreviousInspectionId = payment.PreviousInspectionId };
            db.Appointments.Add(appointment);
            db.Outbox.Add(new() { Kind = "CITA_CONFIRMADA", AggregateId = appointment.Id, Available = clock.GetUtcNow() });
        }
        else
        {
            if (e.Resultado == "APROBADO") { payment.State = "REVERTIDO"; payment.ReversalId ??= Guid.NewGuid(); }
            else if (payment.State == "PENDIENTE") payment.State = e.Resultado;
            if (block?.AttemptId == payment.Id && block.State == "RETENIDO") db.Blocks.Remove(block);
        }
        db.Audit.Add(new() { At = clock.GetUtcNow(), Operation = payment.ReversalId is null ? "PAGO" : "REVERSION", Entity = payment.Id.ToString(), Outcome = payment.State, Correlation = correlation });
        db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = clock.GetUtcNow(), Payload = ApiReply.Serialize(new { centroId = payment.CenterId }) });
        return await State(payment, ct);
    }

    public async Task<PaymentStateResponse> State(PaymentAttempt p, CancellationToken ct)
    {
        var local = db.Appointments.Local.SingleOrDefault(x => x.AttemptId == p.Id);
        var appointmentId = local?.Id ?? await db.Appointments.Where(x => x.AttemptId == p.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(p.Id, p.Id, p.State == "PENDIENTE" && p.Expires <= clock.GetUtcNow() ? "VENCIDO" : p.State, p.Expires, appointmentId, p.ReversalId, p.Amount, p.Currency);
    }
}
