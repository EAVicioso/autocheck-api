// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;
namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public sealed class Data(AutoCheckDb db, AppActor actor, Mutation mutation) : IData
{
    public Task<(int Status, VehicleResponse? Body, string? Location)> Execute(VehicleRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<VehicleResponse>(h, r, async ct =>
        {
            await db.LockAsync("user:" + actor.Id, ct);
            var active = await db.Users.AnyAsync(x => x.Id == actor.Id && x.Active && x.ArchivedAt == null, ct);
            validate(new Snapshot(actor.Id, 1, null, active, await CatalogValid(db, r, ct)));
            var v = new Vehicle
            {
                OwnerId = actor.Id
            };
            Apply(v, r);
            db.Vehicles.Add(v);
            return (201, Response(v), $"/api/vehiculos/{v.Id}");
        });
    }

}
