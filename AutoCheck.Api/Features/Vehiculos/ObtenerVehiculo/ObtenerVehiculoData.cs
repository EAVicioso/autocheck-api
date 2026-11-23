// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public Task<Snapshot?> Read(Query query, CancellationToken ct) => db.Vehicles.AsNoTracking().Where(x => x.Id == query.Id && x.ArchivedAt == null).Select(x => new Snapshot(new VehicleResponse(x.Id, x.Plate, x.Vin, x.Year, x.ModelId, x.ColorId, x.Version, x.ArchivedAt), x.OwnerId == actor.Id || db.Appointments.Any(a => a.VehicleId == x.Id && ((actor.Role == Roles.Inspector && actor.CenterId == a.CenterId && a.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || actor.CenterId == a.CenterId)))))).SingleOrDefaultAsync(ct);
}
