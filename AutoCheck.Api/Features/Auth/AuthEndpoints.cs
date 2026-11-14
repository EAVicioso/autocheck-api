using AutoCheck.Api.Features.Auth.CerrarSesion;
using AutoCheck.Api.Features.Auth.IniciarSesion;
using AutoCheck.Api.Features.Auth.RegistrarActividad;
using AutoCheck.Api.Features.Auth.RegistrarCiudadano;
using AutoCheck.Api.Features.Auth.RenovarSesion;
using AutoCheck.Api.Features.Auth.RestablecerContrasena;
using AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

namespace AutoCheck.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/auth").WithTags("Identidad").RequireRateLimiting("auth");
        g.MapRegistrarCiudadano();
        g.MapIniciarSesion();
        g.MapRenovarSesion();
        g.MapSolicitarRecuperacion();
        g.MapRestablecerContrasena();
        g.MapCerrarSesion();
        g.MapRegistrarActividad();
    }
}
