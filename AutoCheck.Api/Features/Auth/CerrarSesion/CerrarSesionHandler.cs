namespace AutoCheck.Api.Features.Auth.CerrarSesion;

public interface IData
{
    Task Revoke(CancellationToken ct);
}
public sealed class Handler(IData data, IIdentityProvider provider)
{
    public async Task Execute(Command command, CancellationToken ct)
    {
        await data.Revoke(ct);
        await provider.Logout(command.AccessToken, ct);
    }
}
