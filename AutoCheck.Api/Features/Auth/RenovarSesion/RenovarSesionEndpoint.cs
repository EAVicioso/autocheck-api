using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public static class RenovarSesionEndpoint
{
    public static void MapRenovarSesion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/refresh", HandleAsync).AllowAnonymous().Document<SessionResponse>("RenovarSesion", "CU-02 / RS-10").Validated<RefreshRequest>(v => v.RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(4096));
    }

    private static async Task<IResult> HandleAsync(RefreshRequest r, AutoCheckDb db, IIdentityProvider provider, TimeProvider clock, CancellationToken ct)
    {
        var tokens = await provider.Refresh(r.RefreshToken, ct) ?? throw new ApiException(401, "SESION_INVALIDA", "Sesión inválida.");
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tokens.SessionId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.AuthId == tokens.AuthId, ct);
        if (session is null || user is null || session.UserId != user.Id || session.Revoked || !user.Active || user.PasswordResetPending || session.SecurityVersion != user.SessionVersion || clock.GetUtcNow() - session.LastActivity >= TimeSpan.FromHours(1))
        {
            throw new ApiException(401, "SESION_EXPIRADA", "La sesión expiró.");
        }

        return Results.Ok(new SessionResponse(tokens.AccessToken, tokens.RefreshToken, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn)));
    }
}

public sealed record RefreshRequest(string RefreshToken);
