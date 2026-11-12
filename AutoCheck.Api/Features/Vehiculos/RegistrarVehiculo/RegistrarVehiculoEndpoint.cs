using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public static class RegistrarVehiculoEndpoint
{
    public static void MapRegistrarVehiculo(this IEndpointRouteBuilder endpoints)
    {
        Configure(endpoints.MapPost("", async (VehicleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequireRole(Roles.Citizen);
            await CheckCatalog(db, r, ct);
            var v = new Vehicle
            {
                OwnerId = actor.Id
            };
            Apply(v, r);
            db.Vehicles.Add(v);
            return ApiReply.Created($"/api/vehiculos/{v.Id}", Response(v));
        })).Document<VehicleResponse>("RegistrarVehiculo", "IU-01", 201));
    }
}
