
namespace AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo;

public sealed class Data(AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) : IData
{
    public Task<(int Status, object? Body, string? Location)> Execute(Guid id, long version, HttpContext http, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<object>(http, new { id, version }, async ct =>
        {

            await db.LockAsync("user:" + actor.Id, ct);
            await db.LockAsync("vehicle:" + id, ct);
            var item = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            validate(new Snapshot(item.OwnerId, item.Version, item.ArchivedAt, item.ArchivedAt == null && await Lifecycle.HasPending(db, id, false, clock.GetUtcNow(), ct)));
            if (item.ArchivedAt is not null)
            {
                return (204, (object?)null, (string?)null);
            }
            item.ArchivedAt = clock.GetUtcNow();
            item.Version++;

            return (204, (object?)null, (string?)null);
        });
    }

}
