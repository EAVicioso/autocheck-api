using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

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
        var fingerprint = ApiContract.Hash(ApiReply.Serialize(request));
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

        db.Audit.Add(new() { ActorId = actor.Id == Guid.Empty ? null : actor.Id, At = clock.GetUtcNow(), Operation = http.GetEndpoint()?.DisplayName ?? operation, Entity = http.Request.Path, Outcome = "CONFIRMADO", Correlation = http.TraceIdentifier });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return reply;
    }
}
