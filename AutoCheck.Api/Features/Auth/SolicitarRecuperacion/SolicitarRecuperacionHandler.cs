namespace AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

public interface IData
{
    Task Record(CancellationToken ct);
}
public sealed class Handler(IIdentityProvider provider, IData data)
{
    public async Task<RecoveryResponse> Execute(RecoveryRequest request, CancellationToken ct)
    {
        await provider.Recover(ApiContract.NormalizeEmail(request.Correo), ct);
        await data.Record(ct);
        return new("Si la cuenta existe, recibirás instrucciones de recuperación.");
    }
}
public sealed record RecoveryResponse(string Mensaje);
