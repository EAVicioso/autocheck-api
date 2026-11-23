// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public interface IData
{
    Task<OffsetPageResponse<UserResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<OffsetPageResponse<UserResponse>> Execute(Query query, CancellationToken ct)
    {
        actor.RequirePermission(Permissions.Users);
        ApiContract.Filter(query.Rol, Roles.Admin, Roles.Inspector, Roles.Citizen);
        if (query.Nombre?.Length > 120)
        {
            throw ApiContract.Invalid("Nombre demasiado largo.");
        }
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, null, "id", false, "id", "nombre");
        return data.Read(query, settings, ct);
    }
}
