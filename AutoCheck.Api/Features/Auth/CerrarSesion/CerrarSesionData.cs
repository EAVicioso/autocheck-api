// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.CerrarSesion;

public sealed class Data(AutoCheckDb db, AppActor actor, TimeProvider clock, IHttpContextAccessor accessor) : IData
{
    public async Task Revoke(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Sessions.Where(x => x.Id == actor.SessionId && x.UserId == actor.Id && !x.Revoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
        if (changed > 0)
        {
            var http = accessor.HttpContext;
            db.Audit.Add(new() { ActorId = actor.Id, At = clock.GetUtcNow(), Operation = "LOGOUT", Entity = actor.SessionId.ToString(), Outcome = "REVOCADO", Correlation = http?.TraceIdentifier ?? "process", Ip = http?.Connection.RemoteIpAddress });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
