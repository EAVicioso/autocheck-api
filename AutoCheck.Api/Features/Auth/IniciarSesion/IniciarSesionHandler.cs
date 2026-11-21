using AutoCheck.Api.Features.Auth.Common;
namespace AutoCheck.Api.Features.Auth.IniciarSesion;

public interface IData
{
    Task<SessionResponse> Execute(LoginRequest request, HttpContext http, Func<AttemptSnapshot, CancellationToken, Task<ProviderSession?>> authenticate, Func<int, DateTimeOffset, FailureState> failure, Action<SecuritySnapshot> authorize, CancellationToken ct);
}
public sealed class Handler(IData data, IIdentityProvider provider, TimeProvider clock)
{
    public Task<SessionResponse> Execute(LoginRequest request, HttpContext http, CancellationToken ct)
    {
        var normalized = request with { Correo = ApiContract.NormalizeEmail(request.Correo) };
        return data.Execute(normalized, http, (attempt, token) => Authenticate(normalized, attempt, token), Fail, Authorize, ct);
    }
    private Task<ProviderSession?> Authenticate(LoginRequest request, AttemptSnapshot attempt, CancellationToken ct)
    {
        if (attempt.LockedUntil > clock.GetUtcNow())
        {
            throw new ApiException(429, ApiErrorCode.CuentaBloqueada, "La cuenta está bloqueada temporalmente.");
        }
        return provider.Login(request.Correo, request.Contrasena, ct);
    }
    private static FailureState Fail(int current, DateTimeOffset now)
    {
        var count = checked(current + 1);
        return new(count, count >= 5 ? now.AddMinutes(15) : null);
    }
    private static void Authorize(SecuritySnapshot state)
    {
        if (state.ResetPending || state.SessionVersion != state.InitialVersion)
        {
            throw new ApiException(401, ApiErrorCode.SesionExpirada, "La sesión expiró o fue revocada.");
        }
        if (!state.Active || state.ArchivedAt is not null)
        {
            AppActor.Deny();
        }
    }
}
