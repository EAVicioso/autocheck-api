// AutoCheck / INTEC | SRS 1.1 | Source history: Git.


namespace AutoCheck.Api.Features.Usuarios.ArchivarUsuario;

public sealed class Data(AutoCheckDb db, Mutation mutation, AppActor actor, TimeProvider clock) : IData
{
    public Task<(int Status, object? Body, string? Location)> Execute(Guid id, long version, HttpContext http, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<object>(http, new { id, version }, async ct =>
        {
            await db.LockAsync("admin-continuity", ct);
            await db.LockAsync("user:" + id, ct);
            await actor.RevalidateUserManagement(db, clock, ct);
            var item = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            validate(new Snapshot(item.CenterId, item.Version, item.ArchivedAt, item.ArchivedAt == null && await Lifecycle.HasPending(db, id, true, clock.GetUtcNow(), ct), await Lifecycle.IsLastAdministrator(db, item, ct)));
            if (item.ArchivedAt is not null)
            {
                return (204, (object?)null, (string?)null);
            }
            item.ArchivedAt = clock.GetUtcNow();
            item.Version++;
            item.Active = false;
            item.SessionVersion++;
            await db.Sessions.Where(x => x.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
            return (204, (object?)null, (string?)null);
        });
    }

}
