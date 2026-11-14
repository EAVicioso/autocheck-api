using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public static class ListarUsuariosEndpoint
{
    public static void MapListarUsuarios(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (string? rol, bool? activo, HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            actor.RequirePermission(Permissions.Users);
            ApiContract.Filter(rol, Roles.Admin, Roles.Inspector, Roles.Citizen);
            var q = db.Users.AsNoTracking().Where(x => (actor.CenterId == null || x.CenterId == actor.CenterId) && (rol == null || x.Role == rol) && (activo == null || x.Active == activo));
            var page = ApiContract.Page(h);
            var size = ApiContract.Size(h);
            return new
            {
                items = (await q.OrderBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct)).Select(Response),
                pagina = page
            };
        }).Document<UserListResponse>("ListarUsuarios", "CU-11 / PA-11");
    }
}

public sealed record UserListResponse(IEnumerable<UserResponse> Items, int Pagina);
