using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public static class ConsultarCatalogoEndpoint
{
    public static void MapConsultarCatalogo(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/catalogos/{tipo}", HandleAsync).RequireAuthorization("SesionActiva").WithTags("Vehiculos").Document<PageResponse<CatalogResponse>>("ConsultarCatalogo", "IU-01");
    private static async Task<Results<Ok<PageResponse<CatalogResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
