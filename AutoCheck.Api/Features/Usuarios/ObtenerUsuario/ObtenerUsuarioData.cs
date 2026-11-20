using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerUsuario;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<UserResponse?> Read(Query query, CancellationToken ct)
    {
        var result = await db.Users.AsNoTracking().Where(x => x.Id == query.Id && (actor.CenterId == null || x.CenterId == actor.CenterId)).Select(x => new UserResponse(x.Id, x.Email, x.Name, x.Phone, x.Role, x.Permissions.ToImmutableArray(), x.CenterId, x.Active, x.Version, x.ArchivedAt)).SingleOrDefaultAsync(ct);
        if (result is null && await db.Users.AnyAsync(x => x.Id == query.Id, ct))
        {
            AppActor.Deny();
        }
        return result;
    }
}
