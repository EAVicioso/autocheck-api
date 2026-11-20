using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<PageResponse<HistoryResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == query.Id, ct) ?? throw ApiContract.Missing();
        if (!await VehicleSupport.CanRead(db, actor, vehicle, ct))
        {
            AppActor.Deny();
        }
        var range = ApiContract.UtcRange(query.Desde, query.Hasta);
        var q = Authorized(query.Id).Where(x => (query.Resultado == null || x.Result == query.Resultado) && (query.Estado == null || x.State == query.Estado) && (range.From == null || x.Started >= range.From) && (range.To == null || x.Started < range.To));
        q = Seek(q, settings);
        var ordered = settings.Descending ? q.OrderByDescending(x => x.Closed).ThenBy(x => x.Id) : q.OrderBy(x => x.Closed).ThenBy(x => x.Id);
        var rows = await ordered.Skip(settings.Cursor == null ? (settings.Page - 1) * settings.Size : 0).Take(settings.Size + 1).Select(x => new HistoryResponse(x.Id, x.Closed, x.Result, x.RuleVersion, x.MaxMinor, x.Minor, x.Medium, x.Serious, x.CenterId, x.PreviousId)).ToArrayAsync(ct);
        var items = rows.Take(settings.Size).ToArray();
        return new(items.ToImmutableArray(), settings.Cursor == null ? settings.Page : null, rows.Length > settings.Size ? settings.Encode(items[^1].CierreUtc!.Value, items[^1].Id) : null);
    }
    private IQueryable<Inspection> Authorized(Guid id) => db.Inspections.AsNoTracking().Where(x => x.VehicleId == id && x.Closed != null && (x.OwnerId == actor.Id || (actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || x.CenterId == actor.CenterId))));
    private static IQueryable<Inspection> Seek(IQueryable<Inspection> q, PageSettings settings)
    {
        if (settings.Cursor is not { } seek)
        {
            return q;
        }
        return settings.Descending ? q.Where(x => x.Closed < seek.Fecha || (x.Closed == seek.Fecha && x.Id.CompareTo(seek.Id) > 0)) : q.Where(x => x.Closed > seek.Fecha || (x.Closed == seek.Fecha && x.Id.CompareTo(seek.Id) > 0));
    }
}
