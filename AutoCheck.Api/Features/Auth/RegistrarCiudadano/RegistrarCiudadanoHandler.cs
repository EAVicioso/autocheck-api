// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.RegistrarCiudadano;

public interface IData
{
    Task<RegistrationResponse> Execute(RegisterRequest request, HttpContext http, Func<CancellationToken, Task<ProviderRegistration>> createIdentity, CancellationToken ct);
}
public sealed class Handler(IData data, IIdentityProvider provider)
{
    public Task<RegistrationResponse> Execute(RegisterRequest request, HttpContext http, CancellationToken ct)
    {
        var normalized = request with { Correo = ApiContract.NormalizeEmail(request.Correo), Nombre = request.Nombre.Trim() };
        return data.Execute(normalized, http, token => provider.Register(normalized.Correo, normalized.Contrasena, token), ct);
    }
}
