// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck academic prototype, SRS 1.1; RF-CIT-01/02/03, CU-04, PA-03/04.
namespace AutoCheck.Api.Features.Pagos;

public sealed record SandboxEvent(string EventoId, Guid IntentoId, string Resultado);
public sealed record PaymentStateResponse(Guid IntentoId, Guid RetencionId, string Estado, DateTimeOffset ExpiraUtc, Guid? CitaId, Guid? ReversionId, decimal Monto, string Moneda)
{
    public string? SiguienteAccion => Estado == "REVERTIDO" && CitaId is null
        ? "SELECCIONAR_OTRO_HORARIO"
        : null;
}
public interface IPaymentSandbox
{
    Task<SandboxEvent> Process(PaymentAttempt attempt, CancellationToken ct);
}

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
        await db.LockAsync("user:" + payment.UserId, ct);
        await db.LockAsync("vehicle:" + payment.VehicleId, ct);
        await db.Entry(payment).ReloadAsync(ct);
        // The unique-key insert serializes globally, including with workers from before the upgrade.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO autocheck."PaymentEvent" ("Id", "AttemptId", "Outcome")
            VALUES ({e.EventoId}, {e.IntentoId}, {e.Resultado})
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
        var receipt = await db.PaymentEvents.SingleAsync(x => x.Id == e.EventoId, ct);
        if (inserted == 0)
        {
            return await Replay(receipt, e, payment, ct);
        }

        if (payment.State != "APROBADO" && payment.ReversalId is null)
        {
            await ApplyTransition(payment, e.Resultado, correlation, ct);
        }

        var response = await State(payment, ct);
        receipt.ResponseJson = ApiReply.Serialize(response);
        return response;
    }

    private async Task<PaymentStateResponse> Replay(PaymentEvent receipt, SandboxEvent e, PaymentAttempt payment, CancellationToken ct)
    {
        if (receipt.AttemptId != e.IntentoId || receipt.Outcome != e.Resultado)
        {
            throw ApiContract.Conflict("EVENTO_CONFLICTO", "El identificador de evento ya fue utilizado.");
        }

        if (receipt.ResponseJson is { } json)
        {
            return JsonSerializer.Deserialize<PaymentStateResponse>(json, ApiReply.JsonOptions)
                ?? throw ApiContract.Unavailable();
        }

        // Older receipts stored only the input; preserve the first observed replay after upgrade.
        var response = await State(payment, ct);
        receipt.ResponseJson = ApiReply.Serialize(response);
        return response;
    }

    private async Task ApplyTransition(PaymentAttempt payment, string outcome, string correlation, CancellationToken ct)
    {
        var block = await db.Blocks.SingleOrDefaultAsync(
            x => x.RampId == payment.RampId && x.Starts == payment.Starts && x.AttemptId == payment.Id && x.State == "RETENIDO", ct);
        var canConfirm = outcome == "APROBADO" && block is not null && payment.State == "PENDIENTE"
            && await db.Users.AnyAsync(x => x.Id == payment.UserId && x.Active && x.ArchivedAt == null, ct)
            && await db.Vehicles.AnyAsync(x => x.Id == payment.VehicleId && x.OwnerId == payment.UserId && x.ArchivedAt == null, ct);
        var now = clock.GetUtcNow();
        if (canConfirm && block is not null && block.Expires > now)
        {
            Confirm(payment, block, correlation, now);
        }
        else
        {
            Release(payment, block, outcome, correlation, now);
        }

        db.Audit.Add(new()
        {
            At = now, Operation = payment.ReversalId is null ? "PAGO" : "REVERSION",
            Entity = payment.Id.ToString(), Outcome = payment.State, Correlation = correlation,
            After = payment.ReversalId is null ? null : ApiReply.Serialize(new { reversionId = payment.ReversalId })
        });
        if (block is not null)
        {
            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = now, Payload = ApiReply.Serialize(new { centroId = payment.CenterId }) });
        }
    }

    private void Confirm(PaymentAttempt payment, BookingBlock block, string correlation, DateTimeOffset now)
    {
        payment.State = "APROBADO";
        block.State = "CONFIRMADO";
        var appointment = new Appointment
        {
            AttemptId = payment.Id,
            UserId = payment.UserId,
            VehicleId = payment.VehicleId,
            CenterId = payment.CenterId,
            RampId = payment.RampId,
            Starts = payment.Starts,
            PreviousInspectionId = payment.PreviousInspectionId
        };
        db.Appointments.Add(appointment);
        db.Outbox.Add(new() { Kind = "CITA_CONFIRMADA", AggregateId = appointment.Id, Available = now });
        db.Audit.Add(new()
        {
            At = now, Operation = "CONFIRMACION_CITA", Entity = appointment.Id.ToString(),
            Outcome = "CONFIRMADA", Correlation = correlation,
            After = ApiReply.Serialize(new { intentoId = payment.Id })
        });
    }

    private void Release(PaymentAttempt payment, BookingBlock? block, string outcome, string correlation, DateTimeOffset now)
    {
        if (outcome == "APROBADO")
        {
            payment.State = "REVERTIDO";
            payment.ReversalId ??= Guid.NewGuid();
        }
        else if (payment.State == "PENDIENTE")
        {
            payment.State = outcome;
        }

        if (block is null)
        {
            return;
        }

        if (block.Expires <= now)
        {
            db.Audit.Add(new()
            {
                At = now, Operation = "EXPIRACION", Entity = payment.Id.ToString(),
                Outcome = "VENCIDA", Correlation = correlation
            });
        }
        db.Blocks.Remove(block);
    }

    public async Task<PaymentStateResponse> State(PaymentAttempt p, CancellationToken ct)
    {
        var local = db.Appointments.Local.SingleOrDefault(x => x.AttemptId == p.Id);
        var appointmentId = local?.Id ?? await db.Appointments.Where(x => x.AttemptId == p.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(p.Id, p.Id, p.State == "PENDIENTE" && p.Expires <= clock.GetUtcNow() ? "VENCIDO" : p.State, p.Expires, appointmentId, p.ReversalId, p.Amount, p.Currency);
    }
}
