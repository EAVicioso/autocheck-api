using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;

public static class ObtenerVehiculoEndpoint
{
    public static void MapObtenerVehiculo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!await CanRead(db, actor, vehicle, ct))
            {
                AppActor.Deny();
            }

            return Response(vehicle);
        }).Document<VehicleResponse>("ObtenerVehiculo", "CU-10 / PA-10a");
    }
}
