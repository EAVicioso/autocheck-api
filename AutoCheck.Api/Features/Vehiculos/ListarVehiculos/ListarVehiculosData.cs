// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ListarVehiculos;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<PageResponse<VehicleResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var plate = query.Matricula?.Trim().ToUpperInvariant();
        var q = db.Vehicles.AsNoTracking().Where(x => x.OwnerId == actor.Id && (query.IncluirArchivados == true || x.ArchivedAt == null) && (plate == null || x.Plate.Contains(plate)));
        IOrderedQueryable<Vehicle> ordered;
        if (settings.Order == "matricula")
        {
            ordered = settings.Descending ? q.OrderByDescending(x => x.Plate).ThenByDescending(x => x.Id) : q.OrderBy(x => x.Plate).ThenBy(x => x.Id);
        }
        else
        {
            ordered = settings.Descending ? q.OrderByDescending(x => x.Id) : q.OrderBy(x => x.Id);
        }
        var items = await ordered.Skip((settings.Page - 1) * settings.Size).Take(settings.Size).Select(x => new VehicleResponse(x.Id, x.Plate, x.Vin, x.Year, x.ModelId, x.ColorId, x.Version, x.ArchivedAt)).ToArrayAsync(ct);
        return new(items.ToImmutableArray(), settings.Page, null);
    }

}
