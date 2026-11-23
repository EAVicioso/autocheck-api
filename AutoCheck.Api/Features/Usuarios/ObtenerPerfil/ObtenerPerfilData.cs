// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<UserResponse?> Read(CancellationToken ct)
    {
        var result = await db.Users.AsNoTracking().Where(x => x.Id == actor.Id).Select(x => new UserResponse(x.Id, x.Email, x.Name, x.Phone, x.Role, x.Permissions.ToImmutableArray(), x.CenterId, x.Active, x.Version, x.ArchivedAt)).SingleOrDefaultAsync(ct);

        return result;
    }
}
