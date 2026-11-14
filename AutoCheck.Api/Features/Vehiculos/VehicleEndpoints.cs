using AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;
using AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;
using AutoCheck.Api.Features.Vehiculos.ListarVehiculos;
using AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;
using AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

namespace AutoCheck.Api.Features.Vehiculos;

public static class VehicleEndpoints
{
    public static void MapVehicles(this IEndpointRouteBuilder routes)
    {
        routes.MapConsultarCatalogo();
        var g = routes.MapGroup("/api/vehiculos").WithTags("Vehiculos").RequireAuthorization();
        g.MapListarVehiculos();
        g.MapObtenerVehiculo();
        g.MapRegistrarVehiculo();
        g.MapActualizarVehiculo();
    }
}
