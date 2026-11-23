// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public interface IData
{
    Task<SessionResponse> Execute(ProviderSession tokens, HttpContext http, Action<Snapshot> authorize, CancellationToken ct);
}
public sealed class Handler(IData data, IIdentityProvider provider, TimeProvider clock)
{
    public async Task<SessionResponse> Execute(RefreshRequest request, HttpContext http, CancellationToken ct)
    {
        var tokens = await provider.Refresh(request.RefreshToken, ct) ?? throw new ApiException(401, ApiErrorCode.SesionInvalida, "Sesión inválida.");
        return await data.Execute(tokens, http, Authorize, ct);
    }
    private void Authorize(Snapshot state)
    {
        if (!state.Matches || state.Revoked || !state.Active || state.ArchivedAt is not null || state.ResetPending || state.SecurityVersion != state.SessionVersion || clock.GetUtcNow() - state.LastActivity >= TimeSpan.FromHours(1))
        {
            throw new ApiException(401, ApiErrorCode.SesionExpirada, "La sesión expiró.");
        }
    }
}
