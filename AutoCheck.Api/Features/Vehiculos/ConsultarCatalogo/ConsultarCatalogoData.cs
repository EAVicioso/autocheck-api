namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public sealed class Data(AutoCheckDb db) : IData
{
    public async Task<PageResponse<CatalogResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var q = db.Catalog.AsNoTracking().Where(x => x.Type == query.Tipo && (query.PadreId == null || x.ParentId == query.PadreId));
        var ordered = settings.Descending ? q.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id) : q.OrderBy(x => x.Name).ThenBy(x => x.Id);
        var items = await ordered.Skip((settings.Page - 1) * settings.Size).Take(settings.Size).Select(x => new CatalogResponse(x.Id, x.Name, x.ParentId)).ToArrayAsync(ct);
        return new(items.ToImmutableArray(), settings.Page, null);
    }
}
