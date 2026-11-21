using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ListarCertificados;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<PageResponse<CertificateResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var q = db.Certificates.AsNoTracking().Where(x => x.OwnerId == actor.Id && (query.Estado == null || x.State == query.Estado)
            && (query.DesdeUtc == null || x.Issued >= query.DesdeUtc) && (query.HastaUtc == null || x.Issued <= query.HastaUtc)
            && (query.VehiculoId == null || db.Inspections.Any(i => i.Id == x.InspectionId && i.VehicleId == query.VehiculoId)));
        q = Seek(q, settings);
        var ordered = settings.Descending ? q.OrderByDescending(x => x.Issued).ThenByDescending(x => x.Id) : q.OrderBy(x => x.Issued).ThenBy(x => x.Id);
        var rows = await ordered.Skip(settings.Cursor == null ? (settings.Page - 1) * settings.Size : 0).Take(settings.Size + 1)
            .Select(x => new CertificateResponse(x.Id, x.State, x.Issued, x.ManifestHash, x.PdfHash)).ToArrayAsync(ct);
        var items = rows.Take(settings.Size).ToArray();
        return new(items.ToImmutableArray(), settings.Cursor == null ? settings.Page : null, rows.Length > settings.Size ? settings.Encode(items[^1].EmisionUtc, items[^1].Id) : null);
    }

    private static IQueryable<Certificate> Seek(IQueryable<Certificate> q, PageSettings settings)
    {
        if (settings.Cursor is not { } seek)
        {
            return q;
        }
        return settings.Descending ? q.Where(x => x.Issued < seek.Fecha || (x.Issued == seek.Fecha && x.Id.CompareTo(seek.Id) < 0)) : q.Where(x => x.Issued > seek.Fecha || (x.Issued == seek.Fecha && x.Id.CompareTo(seek.Id) > 0));
    }
}
