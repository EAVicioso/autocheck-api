using AutoCheck.Api.Features.Citas.Common;
using static AutoCheck.Api.Features.Citas.Common.AppointmentSupport;

namespace AutoCheck.Api.Features.Citas.ObtenerCita;

public static class ObtenerCitaEndpoint
{
    public static void MapObtenerCita(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var a = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!CanRead(actor, a))
            {
                AppActor.Deny();
            }

            return Response(a);
        }).Document<AppointmentResponse>("ObtenerCita", "CU-03");
    }
}
