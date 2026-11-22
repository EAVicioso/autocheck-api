// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.DescargarCertificado;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public Task<Metadata?> Read(Query query, CancellationToken ct) => db.Certificates.AsNoTracking().Where(x => x.Id == query.Id).Select(x => new Metadata(x.OwnerId == actor.Id, x.State)).SingleOrDefaultAsync(ct);
    public Task<byte[]?> ReadPdf(Query query, CancellationToken ct) => db.Certificates.AsNoTracking().Where(x => x.Id == query.Id && x.OwnerId == actor.Id && x.State == "EMITIDO").Select(x => x.Pdf).SingleOrDefaultAsync(ct);
}
