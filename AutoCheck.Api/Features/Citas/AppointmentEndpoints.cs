using AutoCheck.Api.Features.Citas.AbandonarRetencion;
using AutoCheck.Api.Features.Citas.AsignarInspector;
using AutoCheck.Api.Features.Citas.CancelarCita;
using AutoCheck.Api.Features.Citas.ListarCitas;
using AutoCheck.Api.Features.Citas.ObtenerCita;

namespace AutoCheck.Api.Features.Citas;

public static class AppointmentEndpoints
{
    public static void MapAppointments(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/citas").WithTags("Citas").RequireAuthorization();
        g.MapListarCitas();
        g.MapObtenerCita();
        g.MapAbandonarRetencion();
        g.MapCancelarCita();
        g.MapAsignarInspector();
    }
}
