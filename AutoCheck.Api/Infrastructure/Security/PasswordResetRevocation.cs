namespace AutoCheck.Api.Infrastructure.Security;

// Only PostgreSQL operations. Callers never hold a transaction over Auth HTTP.
public sealed class PasswordResetRevocation(AutoCheckDb db, TimeProvider clock)
{
    public static readonly TimeSpan RecoveryDelay = TimeSpan.FromSeconds(30);

    public async Task<Guid> Prepare(Guid authId, string correlation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("session-security:" + authId, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.AuthId == authId, ct)
            ?? throw new ApiException(401, "RECUPERACION_INVALIDA", "La identidad no está registrada en la aplicación.");
        if (user.PasswordResetPending)
        {
            throw ApiContract.Conflict("RECUPERACION_PENDIENTE", "Hay una recuperación en curso.");
        }
        user.PasswordResetPending = true;
        var operation = new PasswordResetOperation { UserId = user.Id, Started = clock.GetUtcNow(), Correlation = correlation };
        db.PasswordResets.Add(operation);
        // A bounded foreground attempt owns a 30s lease. After a crash, only
        // local revocation is replayed; the secret-dependent Auth call is not.
        db.Outbox.Add(new OutboxMessage { Kind = PasswordResetStates.DeliveryKind, AggregateId = operation.Id, Available = clock.GetUtcNow().Add(RecoveryDelay) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return operation.Id;
    }

    public async Task Finish(Guid operationId, string outcome, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Match dispatcher lock ordering: delivery row, then the security lock.
        var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM autocheck.\"OutboxMessage\" WHERE \"Kind\" = {PasswordResetStates.DeliveryKind} AND \"AggregateId\" = {operationId} FOR UPDATE").SingleAsync(ct);
        await Reconcile(operationId, outcome, ct);
        message.Completed ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    // Dispatcher owns the transaction and delivery-row lock for startup/retry recovery.
    public async Task Reconcile(Guid operationId, string outcome, CancellationToken ct)
    {
        var operation = await db.PasswordResets.SingleAsync(x => x.Id == operationId, ct);
        if (operation.State != PasswordResetStates.Pending)
        {
            return;
        }
        var authId = await db.Users.Where(x => x.Id == operation.UserId).Select(x => x.AuthId).SingleAsync(ct);
        await db.LockAsync("session-security:" + authId, ct);
        var user = await db.Users.SingleAsync(x => x.Id == operation.UserId, ct);
        if (outcome != PasswordResetStates.Rejected)
        {
            await db.Sessions.Where(x => x.UserId == user.Id && !x.Revoked).ExecuteUpdateAsync(update => update.SetProperty(x => x.Revoked, true), ct);
            user.SessionVersion = checked(user.SessionVersion + 1);
        }
        user.PasswordResetPending = false;
        operation.State = outcome;
        operation.Finished = clock.GetUtcNow();
        db.Audit.Add(new AuditEvent
        {
            ActorId = user.Id, At = clock.GetUtcNow(), Entity = user.Id.ToString(), Correlation = operation.Correlation,
            Operation = outcome == PasswordResetStates.Rejected ? "RESTABLECIMIENTO_RECHAZADO" : "RESTABLECIMIENTO_CONTRASENA",
            Outcome = outcome == PasswordResetStates.Completed ? "SESIONES_REVOCADAS" : outcome
        });
    }
}
