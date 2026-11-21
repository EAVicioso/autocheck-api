using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Permisos.ObtenerAcceso;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<AccessResponse?> Read(Query query, CancellationToken ct)
    {
        var result = await db.Users.AsNoTracking().Where(x => x.Id == query.Id && x.ArchivedAt == null && (actor.CenterId == null || x.CenterId == actor.CenterId)).Select(x => new AccessResponse(x.Id, x.Role, x.Permissions.ToImmutableArray(), x.CenterId, x.Active, x.Version, x.ArchivedAt)).SingleOrDefaultAsync(ct);
        if (result is null && await db.Users.AnyAsync(x => x.Id == query.Id && x.ArchivedAt == null, ct))
        {
            AppActor.Deny();
        }
        return result;
    }
}
