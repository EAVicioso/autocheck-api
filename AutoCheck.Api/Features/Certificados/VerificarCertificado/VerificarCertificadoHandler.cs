// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public interface IData
{
    Task<Snapshot?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<VerificationResponse> Execute(Query query, CancellationToken ct)
    {
        var item = await data.Read(query, ct) ?? throw new ApiException(404, ApiErrorCode.CertificadoNoEncontrado, "Certificado no encontrado.");
        if (query.Hash is not { Length: 64 } hash
            || !hash.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f')
            || hash != item.Hash || ApiContract.Hash(item.Manifest) != item.Hash)
        {
            throw ApiContract.Conflict(ApiErrorCode.IntegridadNoValida, "Integridad no válida.");
        }
        return new(item.Id, item.Issued, item.Center, item.Result);
    }
}
