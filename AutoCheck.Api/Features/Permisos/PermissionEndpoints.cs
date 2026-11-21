using AutoCheck.Api.Features.Permisos.ListarRoles;
using AutoCheck.Api.Features.Permisos.ListarPermisos;
using AutoCheck.Api.Features.Permisos.ObtenerAcceso;
using AutoCheck.Api.Features.Permisos.CambiarAcceso;
namespace AutoCheck.Api.Features.Permisos;

public static class PermissionEndpoints
{
    public static void MapPermissions(this IEndpointRouteBuilder routes)
    {
        routes.MapListarRoles();
        routes.MapListarPermisos();
        var group = routes.MapGroup("/api/usuarios").WithTags("Permisos").RequireAuthorization("GestionUsuarios");
        group.MapObtenerAcceso();
        group.MapCambiarAcceso();
    }
}
