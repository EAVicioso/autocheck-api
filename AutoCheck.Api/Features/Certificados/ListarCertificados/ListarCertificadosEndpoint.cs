// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ListarCertificados;

public static class ListarCertificadosEndpoint
{
    public static void MapListarCertificados(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("", HandleAsync).RequireAuthorization().WithTags("Certificados").Document<PageResponse<CertificateResponse>>("ListarCertificados", "CU-06 / PA-06");
    private static async Task<Results<Ok<PageResponse<CertificateResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
