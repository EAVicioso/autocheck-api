using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public static class RegistrarVehiculoEndpoint
{
    public static void MapRegistrarVehiculo(this IEndpointRouteBuilder endpoints)
    {
        Configure(endpoints.MapPost("", HandleAsync).Document<VehicleResponse>("RegistrarVehiculo", "IU-01", 201));
    }
    private static async Task<Results<Created<VehicleResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(VehicleRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(r, h, ct);
        return TypedResults.Created(reply.Location!, reply.Body!);
    }
}
