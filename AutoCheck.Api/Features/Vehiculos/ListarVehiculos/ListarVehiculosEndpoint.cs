using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ListarVehiculos;

public static class ListarVehiculosEndpoint
{
    public static void MapListarVehiculos(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("", HandleAsync).RequireAuthorization().WithTags("Vehiculos").Document<PageResponse<VehicleResponse>>("ListarVehiculos", "IU-01 / PA-10");
    private static async Task<Results<Ok<PageResponse<VehicleResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
