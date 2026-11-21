using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;

public static class ActualizarVehiculoEndpoint
{
    public static void MapActualizarVehiculo(this IEndpointRouteBuilder endpoints)
    {
        Configure(endpoints.MapPatch("/{id:guid}", HandleAsync).Document<VehicleResponse>("ActualizarVehiculo", "IU-01 / PA-05d"));
    }
    private static async Task<Results<Ok<VehicleResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, VehicleRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(id, r, h, ct);
        return TypedResults.Ok(reply.Body!);
    }
}
