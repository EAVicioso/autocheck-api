namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public sealed class Data(AutoCheckDb db) : IData
{
    public async Task<Snapshot?> Read(Query query, CancellationToken ct)
    {
        var value = await db.Certificates.AsNoTracking().Where(c => c.Id == query.Id && c.State == "EMITIDO").Join(db.Inspections.AsNoTracking(), c => c.InspectionId, i => i.Id, (c, i) => new { c.Id, c.Issued, c.Manifest, c.ManifestHash, i.CenterSnapshot, i.Result }).SingleOrDefaultAsync(ct);
        if (value is null)
        {
            return null;
        }
        using var center = JsonDocument.Parse(value.CenterSnapshot);
        return new(value.Id, value.Issued, value.Manifest, value.ManifestHash, center.RootElement.GetProperty("nombre").GetString() ?? "", value.Result ?? "");
    }
}
