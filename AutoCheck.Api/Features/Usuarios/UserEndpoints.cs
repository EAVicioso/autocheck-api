using AutoCheck.Api.Features.Usuarios.ActualizarPerfil;
using AutoCheck.Api.Features.Usuarios.CambiarAcceso;
using AutoCheck.Api.Features.Usuarios.ListarUsuarios;
using AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

namespace AutoCheck.Api.Features.Usuarios;

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/usuarios").WithTags("Usuarios").RequireAuthorization();
        g.MapObtenerPerfil();
        g.MapActualizarPerfil();
        g.MapListarUsuarios();
        g.MapCambiarAcceso();
    }
}
