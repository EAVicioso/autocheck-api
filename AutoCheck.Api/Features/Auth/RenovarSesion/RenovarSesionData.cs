using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public sealed class Data(AutoCheckDb db, TimeProvider clock) : IData
{
    public async Task<SessionResponse> Execute(ProviderSession tokens, HttpContext http, Action<Snapshot> authorize, CancellationToken ct)
    {

        var identity = await (
            from storedSession in db.Sessions.AsNoTracking()
            join account in db.Users.AsNoTracking() on storedSession.UserId equals account.Id
            where storedSession.Id == tokens.SessionId && account.AuthId == tokens.AuthId
            select new { Session = storedSession, User = account }
        ).SingleOrDefaultAsync(ct);
        var session = identity?.Session;
        var user = identity?.User;
        authorize(new Snapshot(identity is not null, session?.Revoked ?? true, user?.Active ?? false, user?.ArchivedAt, user?.PasswordResetPending ?? true, session?.SecurityVersion ?? -1, user?.SessionVersion ?? 0, session?.LastActivity ?? DateTimeOffset.MinValue));

        db.Audit.Add(new() { ActorId = user!.Id, At = clock.GetUtcNow(), Operation = "SESION_RENOVADA", Entity = session!.Id.ToString(), Outcome = "AUTORIZADO", Correlation = http.TraceIdentifier, Ip = http.Connection.RemoteIpAddress });
        await db.SaveChangesAsync(ct);
        return new SessionResponse(tokens.AccessToken, tokens.RefreshToken, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn));
    }
}
