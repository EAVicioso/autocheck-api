using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ObtenerCertificado;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public Task<Snapshot?> Read(Query query, CancellationToken ct) => db.Certificates.AsNoTracking().Where(x => x.Id == query.Id).Select(x => new Snapshot(new CertificateResponse(x.Id, x.State, x.Issued, x.ManifestHash, x.PdfHash), x.OwnerId == actor.Id)).SingleOrDefaultAsync(ct);
}
