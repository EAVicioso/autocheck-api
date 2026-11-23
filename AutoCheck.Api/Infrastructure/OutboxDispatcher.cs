// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck academic prototype, SRS 1.1; PA-04/05/06/13. At-least-once delivery.
using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Certificados.Common;
using AutoCheck.Api.Features.Pagos;
using MongoDB.Bson;
using MongoDB.Driver;
using AutoCheck.Api.Features.Reportes;
using AutoCheck.Api.Features.Reportes.Common;

namespace AutoCheck.Api.Infrastructure;

public sealed class OutboxDispatcher(AutoCheckDb db, TimeProvider clock, IInspectionArchive archive, IAppointmentNotifications notifications, IAvailabilityPublisher availability, IPaymentSandbox sandbox, PaymentWorkflow payments, CertificateIssuer certificates, PasswordResetRevocation passwordResets, ILogger<OutboxDispatcher> logger)
{
    public async Task<bool> DispatchOne(CancellationToken ct, string? kind = null, Guid? aggregateId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var eventKind = kind ?? "";
        var aggregate = aggregateId ?? Guid.Empty;
        var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM autocheck.\"OutboxMessage\" WHERE \"Completed\" IS NULL AND \"FailedAt\" IS NULL AND \"Available\" <= {now} AND ({eventKind} = '' OR \"Kind\" = {eventKind}) AND ({aggregate} = '00000000-0000-0000-0000-000000000000'::uuid OR \"AggregateId\" = {aggregate}) ORDER BY \"Available\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (message is null)
        {
            return false;
        }

        try
        {
            var outcome = "ENTREGADO";
            switch (message.Kind)
            {
                case PasswordResetStates.DeliveryKind:
                    await passwordResets.Reconcile(message.AggregateId, PasswordResetStates.Uncertain, ct);
                    break;
                case "PAGO_SANDBOX":
                    var p = await db.Payments.SingleOrDefaultAsync(x => x.Id == message.AggregateId, ct)
                        ?? throw new MissingOutboxAggregateException();
                    var e = await sandbox.Process(p, ct);
                    await payments.Apply(e, message.Id.ToString(), ct);
                    break;
                case "COPIA_MONGO":
                    var inspection = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == message.AggregateId, ct)
                        ?? throw new MissingOutboxAggregateException();
                    await archive.Copy(inspection, ct);
                    break;
                case "CERTIFICADO":
                    await db.LockAsync("inspection:" + message.AggregateId, ct);
                    await certificates.Complete(message.AggregateId, ct);
                    break;
                case "CITA_CONFIRMADA":
                case "CITA_CANCELADA":
                    await db.LockAsync("notification:" + message.AggregateId, ct);
                    var appointment = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == message.AggregateId, ct)
                        ?? throw new MissingOutboxAggregateException();
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
                        var root = payload.RootElement;
                        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("centroId", out var center)
                            || center.ValueKind != JsonValueKind.String || !center.TryGetGuid(out var centerId) || centerId == Guid.Empty)
                        {
                            throw new JsonException("Invalid availability payload.");
                        }
                        await availability.Publish(centerId, ct);
                    }

                    break;
                default:
                    throw new JsonException("Unknown outbox event type.");
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
            // Roll back the delivery before making the conditional failure update.
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            await RecordFailure(message, ex, ct);
            return true;
        }
    }

    private async Task RecordFailure(OutboxMessage message, Exception exception, CancellationToken ct)
    {
        var nextAttempts = message.Attempts + 1;
        var pending = db.Outbox.Where(x => x.Id == message.Id && x.Completed == null && x.FailedAt == null
            && x.Attempts == message.Attempts && x.Available == message.Available);
        var failedAt = clock.GetUtcNow();
        var failureCode = PermanentFailure(exception);
        if (failureCode is not null)
        {
            // PostgreSQL infinity keeps older workers from selecting a dead letter during rollout.
            var failed = await pending.ExecuteUpdateAsync(updates => updates.SetProperty(x => x.Attempts, nextAttempts)
                .SetProperty(x => x.Available, DateTimeOffset.MaxValue)
                .SetProperty(x => x.FailedAt, failedAt).SetProperty(x => x.FailureCode, failureCode), ct);
            if (failed > 0)
            {
                ApiLog.DeliveryFailed(logger, message.Id, failureCode);
            }
            return;
        }

        var retryAt = failedAt.AddSeconds(Math.Pow(2, Math.Min(nextAttempts, 8)));
        var rescheduled = await pending.ExecuteUpdateAsync(updates => updates.SetProperty(x => x.Attempts, nextAttempts)
            .SetProperty(x => x.Available, retryAt), ct);
        if (rescheduled > 0)
        {
            ApiLog.DeliveryRetry(logger, message.Id, exception.GetType().Name);
        }
    }

    private static string? PermanentFailure(Exception exception) => exception switch
    {
        MissingOutboxAggregateException => "AGREGADO_NO_ENCONTRADO",
        JsonException => "PAYLOAD_INVALIDO",
        BsonSerializationException => "BSON_INVALIDO",
        MongoWriteException { WriteError.Code: 121 } => "ESQUEMA_MONGO_INVALIDO",
        _ => null
    };

    public sealed class MissingOutboxAggregateException() : Exception("Outbox aggregate was not found in PostgreSQL.");
}

public sealed class BackgroundDelivery(IServiceScopeFactory scopes, IConfiguration config, TimeProvider clock, ILogger<BackgroundDelivery> logger) : BackgroundService
{
    private static readonly string[] DeliveryKinds = ["PAGO_SANDBOX", "CERTIFICADO", "COPIA_MONGO", "CITA_CONFIRMADA", "CITA_CANCELADA", "DISPONIBILIDAD", PasswordResetStates.DeliveryKind];
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
