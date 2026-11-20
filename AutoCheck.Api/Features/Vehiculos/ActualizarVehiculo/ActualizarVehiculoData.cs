using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;
namespace AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;

public sealed class Data(AutoCheckDb db, AppActor actor, Mutation mutation) : IData
{
    public Task<(int Status, VehicleResponse? Body, string? Location)> Execute(Guid id, VehicleRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<VehicleResponse>(h, r, async ct =>
        {
            await db.LockAsync("user:" + actor.Id, ct);
            await db.LockAsync("vehicle:" + id, ct);
            var v = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            var active = await db.Users.AnyAsync(x => x.Id == actor.Id && x.Active && x.ArchivedAt == null, ct);
            validate(new Snapshot(v.OwnerId, v.Version, v.ArchivedAt, active, await CatalogValid(db, r, ct)));
            v.Version++;
            Apply(v, r);
            return (200, Response(v), (string?)null);
        });
    }

}
