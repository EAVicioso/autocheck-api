// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.DescargarCertificado;

public interface IData
{
    Task<Metadata?> Read(Query query, CancellationToken ct);
    Task<byte[]?> ReadPdf(Query query, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<DownloadResponse> Execute(Query query, CancellationToken ct)
    {
        var metadata = await data.Read(query, ct) ?? throw ApiContract.Missing();
        if (!metadata.Owns)
        {
            AppActor.Deny();
        }
        if (metadata.State != "EMITIDO")
        {
            throw ApiContract.Conflict(ApiErrorCode.CertificadoPendiente, "El certificado aún no está disponible.");
        }
        var bytes = await data.ReadPdf(query, ct) ?? throw ApiContract.Unavailable();
        return new(bytes, $"AutoCheck-{query.Id}.pdf");
    }
}
