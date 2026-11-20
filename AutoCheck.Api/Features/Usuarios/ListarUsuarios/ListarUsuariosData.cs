using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public sealed class Data(AutoCheckDb db, AppActor actor) : IData
{
    public async Task<PageResponse<UserResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking().Where(x => (query.IncluirArchivados == true || x.ArchivedAt == null) && (actor.CenterId == null || x.CenterId == actor.CenterId)
            && (query.Rol == null || x.Role == query.Rol) && (query.Activo == null || x.Active == query.Activo) && (query.Nombre == null || x.Name.Contains(query.Nombre)));
        IOrderedQueryable<UserAccount> ordered;
        if (settings.Order == "nombre")
        {
            ordered = settings.Descending ? q.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id) : q.OrderBy(x => x.Name).ThenBy(x => x.Id);
        }
        else
        {
            ordered = settings.Descending ? q.OrderByDescending(x => x.Id) : q.OrderBy(x => x.Id);
        }
        var items = await ordered.Skip((settings.Page - 1) * settings.Size).Take(settings.Size)
            .Select(x => new UserResponse(x.Id, x.Email, x.Name, x.Phone, x.Role, x.Permissions.ToImmutableArray(), x.CenterId, x.Active, x.Version, x.ArchivedAt)).ToArrayAsync(ct);
        return new(items.ToImmutableArray(), settings.Page, null);
    }

}
