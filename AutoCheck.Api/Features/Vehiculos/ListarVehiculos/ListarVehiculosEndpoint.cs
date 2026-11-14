using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.ListarVehiculos;

public static class ListarVehiculosEndpoint
{
    public static void MapListarVehiculos(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) => new { items = (await db.Vehicles.AsNoTracking().Where(x => x.OwnerId == actor.Id).OrderBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response) }).Document<ItemsResponse<VehicleResponse>>("ListarVehiculos", "IU-01 / PA-10a");
    }
}
