namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public interface IData
{
    Task<Snapshot?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<VerificationResponse> Execute(Query query, CancellationToken ct)
    {
        if (query.Hash.Length != 64 || !query.Hash.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw ApiContract.Invalid("Hash SHA-256 inválido.");
        }
        var item = await data.Read(query, ct) ?? throw new ApiException(404, ApiErrorCode.CertificadoNoEncontrado, "Certificado no encontrado.");
        if (query.Hash != item.Hash || ApiContract.Hash(item.Manifest) != item.Hash)
        {
            throw ApiContract.Conflict(ApiErrorCode.IntegridadNoValida, "Integridad no válida.");
        }
        return new(item.Id, item.Issued, item.Center, item.Result, "VALIDA");
    }
}
