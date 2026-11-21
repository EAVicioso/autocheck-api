using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public static class ConsultarHistorialEndpoint
{
    public static void MapConsultarHistorial(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/vehiculos/{id:guid}/historial", HandleAsync).RequireAuthorization("SesionActiva").WithTags("Historial").Document<PageResponse<HistoryResponse>>("ConsultarHistorial", "CU-10 / PA-10 / RS-08");
    private static async Task<Results<Ok<PageResponse<HistoryResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
