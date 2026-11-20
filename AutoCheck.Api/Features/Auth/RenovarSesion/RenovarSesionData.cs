using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public sealed class Data(AutoCheckDb db, TimeProvider clock) : IData
{
    public async Task<SessionResponse> Execute(ProviderSession tokens, HttpContext http, Action<Snapshot> authorize, CancellationToken ct)
    {

        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tokens.SessionId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.AuthId == tokens.AuthId, ct);
        authorize(new Snapshot(session is not null && user is not null && session.UserId == user.Id, session?.Revoked ?? true, user?.Active ?? false, user?.ArchivedAt, user?.PasswordResetPending ?? true, session?.SecurityVersion ?? -1, user?.SessionVersion ?? 0, session?.LastActivity ?? DateTimeOffset.MinValue));

        db.Audit.Add(new() { ActorId = user!.Id, At = clock.GetUtcNow(), Operation = "SESION_RENOVADA", Entity = session!.Id.ToString(), Outcome = "AUTORIZADO", Correlation = http.TraceIdentifier, Ip = http.Connection.RemoteIpAddress });
        await db.SaveChangesAsync(ct);
        return new SessionResponse(tokens.AccessToken, tokens.RefreshToken, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn));
    }
}
