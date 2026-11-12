using AutoCheck.Api.Features.Inspecciones.CerrarInspeccion;
using AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;
using AutoCheck.Api.Features.Inspecciones.ConsultarReglas;
using AutoCheck.Api.Features.Inspecciones.ConsultarReinspecciones;
using AutoCheck.Api.Features.Inspecciones.CrearVersionReglas;
using AutoCheck.Api.Features.Inspecciones.IniciarInspeccion;
using AutoCheck.Api.Features.Inspecciones.ListarInspecciones;
using AutoCheck.Api.Features.Inspecciones.ObtenerInspeccion;
using AutoCheck.Api.Features.Inspecciones.RegistrarDefectos;

namespace AutoCheck.Api.Features.Inspecciones;

public static class InspectionEndpoints
{
    public static void MapInspections(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/inspecciones").WithTags("Inspecciones").RequireAuthorization();
        g.MapListarInspecciones();
        g.MapIniciarInspeccion();
        g.MapObtenerInspeccion();
        g.MapRegistrarDefectos();
        g.MapCerrarInspeccion();
        g.MapConsultarReinspecciones();
        routes.MapConsultarHistorial();
        var rules = routes.MapGroup("/api/configuracion/reglas-inspeccion").WithTags("Reglas").RequireAuthorization();
        rules.MapConsultarReglas();
        rules.MapCrearVersionReglas();
    }
}
