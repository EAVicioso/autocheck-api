using AutoCheck.Api.Features.Centros.ActualizarCentro;
using AutoCheck.Api.Features.Centros.ActualizarRampa;
using AutoCheck.Api.Features.Centros.ConfigurarHorarios;
using AutoCheck.Api.Features.Centros.ConsultarDisponibilidad;
using AutoCheck.Api.Features.Centros.CrearCentro;
using AutoCheck.Api.Features.Centros.CrearRampa;
using AutoCheck.Api.Features.Centros.ListarCentros;
using AutoCheck.Api.Features.Centros.ObtenerCentro;

namespace AutoCheck.Api.Features.Centros;

public static class CenterEndpoints
{
    public static void MapCenters(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/centros").WithTags("Centros").RequireAuthorization();
        g.MapListarCentros();
        g.MapObtenerCentro();
        g.MapCrearCentro();
        g.MapActualizarCentro();
        g.MapCrearRampa();
        g.MapActualizarRampa();
        g.MapConfigurarHorarios();
        g.MapConsultarDisponibilidad();
    }
}
