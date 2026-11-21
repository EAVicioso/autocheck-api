using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ObtenerCertificado;

public static class ObtenerCertificadoEndpoint
{
    public static void MapObtenerCertificado(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}", HandleAsync).RequireAuthorization("SesionActiva").Document<CertificateResponse>("ObtenerCertificado", "CU-06");
    private static async Task<Results<Ok<CertificateResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
