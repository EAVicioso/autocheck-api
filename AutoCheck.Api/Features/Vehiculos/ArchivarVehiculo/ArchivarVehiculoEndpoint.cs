using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo;

public static class ArchivarVehiculoEndpoint
{
    public static void MapArchivarVehiculo(this IEndpointRouteBuilder endpoints) => endpoints.MapDelete("/{id:guid}", HandleAsync).RequireAuthorization("Ciudadano").WithName("ArchivarVehiculo").WithSummary("DEC-LIFECYCLE-01 / RS-03").Produces(204).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, long version, HttpContext http, Handler handler, CancellationToken ct)
    {
        _ = await handler.Execute(new Command(id, version), http, ct);
        return TypedResults.NoContent();
    }
}
