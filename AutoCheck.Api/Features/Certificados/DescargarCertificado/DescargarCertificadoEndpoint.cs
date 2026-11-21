using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Certificados.DescargarCertificado;

public static class DescargarCertificadoEndpoint
{
    public static void MapDescargarCertificado(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}/pdf", HandleAsync).RequireAuthorization("SesionActiva").WithName("DescargarCertificado").WithSummary("CU-06 / PA-06/07").Produces(200, contentType: "application/pdf").ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, HttpContext http, Handler handler, CancellationToken ct)
    {
        var result = await handler.Execute(query, ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.File(result.Content, "application/pdf", result.FileName, enableRangeProcessing: false);
    }
}
