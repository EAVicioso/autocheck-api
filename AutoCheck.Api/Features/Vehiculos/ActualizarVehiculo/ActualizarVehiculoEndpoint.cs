using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;

public static class ActualizarVehiculoEndpoint
{
    public static void MapActualizarVehiculo(this IEndpointRouteBuilder endpoints)
    {
        Configure(endpoints.MapPatch("/{id:guid}", async (Guid id, VehicleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            await db.LockAsync("vehicle:" + id, ct);
            var v = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (v.OwnerId != actor.Id)
            {
                AppActor.Deny();
            }

            await CheckCatalog(db, r, ct);
            Apply(v, r);
            return ApiReply.Ok(Response(v));
        })).Document<VehicleResponse>("ActualizarVehiculo", "IU-01 / PA-05d"));
    }
}
