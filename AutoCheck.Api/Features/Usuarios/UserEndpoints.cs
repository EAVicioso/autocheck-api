// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.ActualizarPerfil;
using AutoCheck.Api.Features.Usuarios.ListarUsuarios;
using AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

namespace AutoCheck.Api.Features.Usuarios;

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/usuarios").WithTags("Usuarios").RequireAuthorization("SesionActiva");
        g.MapObtenerPerfil();
        AutoCheck.Api.Features.Usuarios.ObtenerUsuario.ObtenerUsuarioEndpoint.MapObtenerUsuario(g);
        AutoCheck.Api.Features.Usuarios.ArchivarUsuario.ArchivarUsuarioEndpoint.MapArchivarUsuario(g);
        g.MapActualizarPerfil();
        g.MapListarUsuarios();
    }
}
