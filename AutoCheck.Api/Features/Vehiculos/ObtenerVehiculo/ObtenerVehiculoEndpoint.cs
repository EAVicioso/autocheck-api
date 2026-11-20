using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;

public static class ObtenerVehiculoEndpoint
{
    public static void MapObtenerVehiculo(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}", HandleAsync).Document<VehicleResponse>("ObtenerVehiculo", "CU-10 / PA-10");
    private static async Task<Results<Ok<VehicleResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
