// AutoCheck academic prototype, SRS 1.1; PA-04/05/06/13. At-least-once delivery.
using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Certificados.Common;
using AutoCheck.Api.Features.Pagos;
using AutoCheck.Api.Features.Reportes;
using AutoCheck.Api.Features.Reportes.Common;

namespace AutoCheck.Api.Infrastructure;

public sealed class OutboxDispatcher(AutoCheckDb db, TimeProvider clock, IInspectionArchive archive, IAppointmentNotifications notifications, IAvailabilityPublisher availability, IPaymentSandbox sandbox, PaymentWorkflow payments, CertificateIssuer certificates, ILogger<OutboxDispatcher> logger)
{
    public async Task<bool> DispatchOne(CancellationToken ct, string? kind = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var eventKind = kind ?? "";
        var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM autocheck.\"OutboxMessage\" WHERE \"Completed\" IS NULL AND \"Available\" <= {now} AND ({eventKind} = '' OR \"Kind\" = {eventKind}) ORDER BY \"Available\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (message is null)
        {
            return false;
        }

        try
        {
            var outcome = "ENTREGADO";
            switch (message.Kind)
            {
                case "PAGO_SANDBOX":
                    var p = await db.Payments.SingleAsync(x => x.Id == message.AggregateId, ct);
                    var e = await sandbox.Process(p, ct);
                    await payments.Apply(e, message.Id.ToString(), ct);
                    break;
                case "COPIA_MONGO":
                    await archive.Copy(await db.Inspections.AsNoTracking().SingleAsync(x => x.Id == message.AggregateId, ct), ct);
                    break;
                case "CERTIFICADO":
                    await db.LockAsync("inspection:" + message.AggregateId, ct);
                    await certificates.Complete(message.AggregateId, ct);
                    break;
                case "CITA_CONFIRMADA":
                case "CITA_CANCELADA":
                    await db.LockAsync("notification:" + message.AggregateId, ct);
                    var appointment = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == message.AggregateId, ct);
                    if (message.Kind == "CITA_CONFIRMADA" && appointment.State == "CANCELADA")
                    {
                        outcome = "SUPERADO_POR_CANCELACION";
                    }
                    else
                    {
                        await notifications.Send(appointment, message.Kind, message.Id, ct);
                    }

                    break;
                case "DISPONIBILIDAD":
                    using (var payload = JsonDocument.Parse(message.Payload))
                    {
                        await availability.Publish(payload.RootElement.GetProperty("centroId").GetGuid(), ct);
                    }

                    break;
                default:
                    throw new InvalidOperationException("Unknown outbox event type.");
            }

            message.Completed = clock.GetUtcNow();
            db.Audit.Add(new() { At = clock.GetUtcNow(), Operation = message.Kind, Entity = message.AggregateId.ToString(), Outcome = outcome, Correlation = message.Id.ToString() });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Roll back all tracked side effects before recording the retry in a separate transaction.
            var messageId = message.Id;
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var failed = await db.Outbox.SingleAsync(x => x.Id == messageId, ct);
            failed.Attempts++;
            failed.Available = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(failed.Attempts, 8))));
            await db.SaveChangesAsync(ct);
            ApiLog.DeliveryRetry(logger, messageId, ex.GetType().Name);
            return true;
        }
    }
}

public sealed class BackgroundDelivery(IServiceScopeFactory scopes, IConfiguration config, TimeProvider clock, ILogger<BackgroundDelivery> logger) : BackgroundService
{
    private static readonly string[] DeliveryKinds = ["PAGO_SANDBOX", "CERTIFICADO", "COPIA_MONGO", "CITA_CONFIRMADA", "CITA_CANCELADA", "DISPONIBILIDAD"];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Workers:Enabled", true) || string.IsNullOrWhiteSpace(config.GetConnectionString("Postgres")))
        {
            return;
        }

        // Independent lanes prevent an unavailable Atlas/email/Realtime provider from delaying PDF issuance or payment processing.
        var tasks = DeliveryKinds.Select(kind => DeliveryLoop(kind, stoppingToken)).Append(Housekeeping(stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task DeliveryLoop(string kind, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                for (var n = 0; n < 20; n++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(ct, kind))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ApiLog.DeliveryUnavailable(logger, kind, ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, ct);
        }
    }

    private async Task Housekeeping(CancellationToken stoppingToken)
    {
        var nextReports = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var expirationScope = scopes.CreateScope();
                await Expire(expirationScope.ServiceProvider.GetRequiredService<AutoCheckDb>(), stoppingToken);
                if (clock.GetUtcNow() >= nextReports)
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ReportConsolidator>().Run(stoppingToken);
                    nextReports = clock.GetUtcNow().AddSeconds(30);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ApiLog.BackgroundUnavailable(logger, ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken);
        }
    }

    private async Task Expire(AutoCheckDb db, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = await db.Blocks.AsNoTracking().Where(x => x.State == "RETENIDO" && x.Expires <= now).Select(x => new { x.RampId, x.Starts }).Take(100).ToListAsync(ct);
        foreach (var item in expired)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var center = await db.Ramps.Where(x => x.Id == item.RampId).Select(x => x.CenterId).SingleAsync(ct);
            await db.LockAsync("center:" + center, ct);
            var b = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == item.RampId && x.Starts == item.Starts, ct);
            if (b is not null && b.State == "RETENIDO" && b.Expires <= now)
            {
                db.Audit.Add(new() { At = now, Operation = "EXPIRACION", Entity = b.AttemptId.ToString(), Outcome = "VENCIDA", Correlation = Guid.NewGuid().ToString() });
                db.Blocks.Remove(b);
                db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = now, Payload = ApiReply.Serialize(new { centroId = center }) });
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
    }
}
