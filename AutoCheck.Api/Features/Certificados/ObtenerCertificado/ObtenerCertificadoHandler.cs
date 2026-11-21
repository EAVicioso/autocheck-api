using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ObtenerCertificado;

public interface IData
{
    Task<Snapshot?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<CertificateResponse> Execute(Query query, CancellationToken ct)
    {
        var snapshot = await data.Read(query, ct) ?? throw ApiContract.Missing();
        if (!snapshot.Owns)
        {
            AppActor.Deny();
        }
        return snapshot.Response;
    }
}
