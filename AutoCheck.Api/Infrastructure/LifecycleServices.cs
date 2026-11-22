// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Infrastructure;

public static class LifecycleServices
{
    public static IServiceCollection AddLifecycleSlices(this IServiceCollection services)
    {
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ListarUsuarios.IData, AutoCheck.Api.Features.Usuarios.ListarUsuarios.Data>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ListarUsuarios.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ListarVehiculos.IData, AutoCheck.Api.Features.Vehiculos.ListarVehiculos.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ListarVehiculos.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.ListarCertificados.IData, AutoCheck.Api.Features.Certificados.ListarCertificados.Data>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.ListarCertificados.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auditorias.ConsultarAuditorias.IData, AutoCheck.Api.Features.Auditorias.ConsultarAuditorias.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auditorias.ConsultarAuditorias.Handler>();
        services.AddScoped<SecurityAudit>();
        services.AddScoped<AutoCheck.Api.Features.Auth.CerrarSesion.IData, AutoCheck.Api.Features.Auth.CerrarSesion.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.CerrarSesion.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo.IData, AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Inspecciones.ConsultarHistorial.IData, AutoCheck.Api.Features.Inspecciones.ConsultarHistorial.Data>();
        services.AddScoped<AutoCheck.Api.Features.Inspecciones.ConsultarHistorial.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ActualizarPerfil.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.CambiarAcceso.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ArchivarUsuario.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.SolicitarCertificado.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auth.IniciarSesion.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RegistrarCiudadano.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RenovarSesion.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auth.SolicitarRecuperacion.IData, AutoCheck.Api.Features.Auth.SolicitarRecuperacion.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.SolicitarRecuperacion.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RegistrarActividad.IData, AutoCheck.Api.Features.Auth.RegistrarActividad.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RegistrarActividad.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ObtenerPerfil.IData, AutoCheck.Api.Features.Usuarios.ObtenerPerfil.Data>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ObtenerPerfil.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ObtenerUsuario.IData, AutoCheck.Api.Features.Usuarios.ObtenerUsuario.Data>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ObtenerUsuario.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.ObtenerAcceso.IData, AutoCheck.Api.Features.Permisos.ObtenerAcceso.Data>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.ObtenerAcceso.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo.IData, AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.ObtenerCertificado.IData, AutoCheck.Api.Features.Certificados.ObtenerCertificado.Data>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.ObtenerCertificado.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.DescargarCertificado.IData, AutoCheck.Api.Features.Certificados.DescargarCertificado.Data>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.DescargarCertificado.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.ListarRoles.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.ListarPermisos.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.VerificarCertificado.IData, AutoCheck.Api.Features.Certificados.VerificarCertificado.Data>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.VerificarCertificado.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Auditorias.ObtenerAuditoria.IData, AutoCheck.Api.Features.Auditorias.ObtenerAuditoria.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auditorias.ObtenerAuditoria.Handler>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ActualizarPerfil.IData, AutoCheck.Api.Features.Usuarios.ActualizarPerfil.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo.IData, AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo.Data>();
        services.AddScoped<AutoCheck.Api.Features.Usuarios.ArchivarUsuario.IData, AutoCheck.Api.Features.Usuarios.ArchivarUsuario.Data>();
        services.AddScoped<AutoCheck.Api.Features.Permisos.CambiarAcceso.IData, AutoCheck.Api.Features.Permisos.CambiarAcceso.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo.IData, AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo.Data>();
        services.AddScoped<AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo.IData, AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo.Data>();
        services.AddScoped<AutoCheck.Api.Features.Certificados.SolicitarCertificado.IData, AutoCheck.Api.Features.Certificados.SolicitarCertificado.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RegistrarCiudadano.IData, AutoCheck.Api.Features.Auth.RegistrarCiudadano.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.IniciarSesion.IData, AutoCheck.Api.Features.Auth.IniciarSesion.Data>();
        services.AddScoped<AutoCheck.Api.Features.Auth.RenovarSesion.IData, AutoCheck.Api.Features.Auth.RenovarSesion.Data>();
        return services;
    }
}
