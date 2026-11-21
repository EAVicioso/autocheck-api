using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ListarCertificados;

public interface IData
{
    Task<PageResponse<CertificateResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public Task<PageResponse<CertificateResponse>> Execute(Query query, CancellationToken ct)
    {
        ApiContract.Filter(query.Estado, "PENDIENTE", "EMITIDO");
        if (query.DesdeUtc > query.HastaUtc)
        {
            throw ApiContract.Invalid("Rango de fechas inválido.");
        }
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, query.Cursor, "emisionUtc", true, "emisionUtc");
        return data.Read(query with { DesdeUtc = query.DesdeUtc?.ToUniversalTime(), HastaUtc = query.HastaUtc?.ToUniversalTime() }, settings, ct);
    }
}
