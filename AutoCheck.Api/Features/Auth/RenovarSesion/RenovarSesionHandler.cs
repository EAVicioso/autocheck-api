// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.


namespace AutoCheck.Api.Features.Auth;

public sealed record RefreshRequest(string RefreshToken);

public static partial class AuthEndpoints
{
    private static async Task<IResult> Refresh(RefreshRequest r, AutoCheckDb db, IIdentityProvider provider, TimeProvider clock, CancellationToken ct)
    {
        var tokens = await provider.Refresh(r.RefreshToken, ct) ?? throw new ApiException(401, "SESION_INVALIDA", "Sesión inválida.");
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tokens.SessionId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.AuthId == tokens.AuthId, ct);
        if (session is null || user is null || session.UserId != user.Id || session.Revoked || !user.Active || clock.GetUtcNow() - session.LastActivity >= TimeSpan.FromHours(1))
            throw new ApiException(401, "SESION_EXPIRADA", "La sesión expiró.");
        return Results.Ok(new SessionResponse(tokens.AccessToken, tokens.RefreshToken, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn)));
    }
}
