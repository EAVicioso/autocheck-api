// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Vehiculos.Common;

namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public static class RegistrarVehiculoEndpoint
{
    public static void MapRegistrarVehiculo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("", HandleAsync).WithMetadata(new MutationAudit("VEHICULO_REGISTRADO")).Document<VehicleResponse>("RegistrarVehiculo", "IU-01", 201).Interactive().Validated<RegisterVehicleRequest>();
    }
    private static async Task<Results<Created<VehicleResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(RegisterVehicleRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(r, h, ct);
        return TypedResults.Created(reply.Location!, reply.Body!);
    }
}
