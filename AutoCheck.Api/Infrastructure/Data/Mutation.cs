// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed record MutationAudit(string Operation, bool UseUserId = false, bool SkipUnchanged = false);

public sealed class Mutation(AutoCheckDb db, AppActor actor, TimeProvider clock)
{
    public async Task<ApiReply> Run(HttpContext http, object request, Func<CancellationToken, Task<ApiReply>> action, bool idempotent = false)
    {
        var ct = http.RequestAborted;
        var key = http.Request.Headers["Idempotency-Key"].ToString();
        if (idempotent && (string.IsNullOrWhiteSpace(key) || key.Length > 128))
        {
            throw ApiContract.Invalid("Idempotency-Key es obligatorio y admite hasta 128 caracteres.");
        }

        var operation = $"{http.Request.Method}:{http.Request.Path}";
        var storageKey = ApiContract.Hash($"{actor.Id}:{operation}:{key}");
        var fingerprint = idempotent ? ApiContract.Hash(ApiReply.Serialize(request)) : "";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (idempotent)
        {
            await db.LockAsync("idempotency:" + storageKey, ct);
            var previous = await db.Idempotency.FindAsync([storageKey], ct);
            if (previous is not null)
            {
                if (previous.Fingerprint != fingerprint)
                {
                    throw ApiContract.Conflict("IDEMPOTENCIA_CONFLICTO", "La clave fue utilizada con otra solicitud.");
                }

                return new(previous.Status, previous.Json, previous.Location);
            }
        }

        var reply = await action(ct);
        if (idempotent)
        {
            db.Idempotency.Add(new() { Key = storageKey, Fingerprint = fingerprint, Status = reply.Status, Json = reply.Json, Location = reply.Location });
        }

        db.ChangeTracker.DetectChanges();
        var metadata = http.GetEndpoint()?.Metadata;
        var name = metadata?.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName ?? operation;
        var audit = metadata?.GetMetadata<MutationAudit>() ?? new MutationAudit(name);
        if (audit.SkipUnchanged && !db.ChangeTracker.HasChanges())
        {
            return reply;
        }

        RecordAudit(http, audit);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return reply;
    }
    // Typed callers retain DTOs on the normal path; only idempotent replays deserialize persisted JSON.
    public async Task<(int Status, T? Body, string? Location)> RunTyped<T>(HttpContext http, object request, Func<CancellationToken, Task<(int Status, T? Body, string? Location)>> action, bool idempotent = false) where T : class
    {
        (int Status, T? Body, string? Location) current = default;
        var executed = false;
        var reply = await Run(http, request, async ct =>
        {
            current = await action(ct);
            executed = true;
            return new ApiReply(current.Status, idempotent ? ApiReply.Serialize(current.Body!) : "", current.Location);
        }, idempotent);
        return executed ? current : (reply.Status, JsonSerializer.Deserialize<T>(reply.Json, ApiReply.JsonOptions), reply.Location);
    }

    private void RecordAudit(HttpContext http, MutationAudit audit)
    {
        var changed = db.ChangeTracker.Entries().FirstOrDefault(x => x.Entity is UserAccount or Vehicle && x.State is EntityState.Modified or EntityState.Added);
        db.Audit.Add(new()
        {
            ActorId = actor.Id == Guid.Empty ? null : actor.Id,
            At = clock.GetUtcNow(),
            Operation = audit.Operation,
            Entity = audit.UseUserId && changed?.Entity is UserAccount user
                ? user.Id.ToString()
                : http.Request.Path,
            Outcome = "CONFIRMADO",
            Correlation = http.TraceIdentifier,
            Ip = http.Connection.RemoteIpAddress,
            Before = changed?.State == EntityState.Modified ? AuditSnapshot.Create(changed, true) : null,
            After = changed is null ? null : AuditSnapshot.Create(changed, false)
        });
    }
}
