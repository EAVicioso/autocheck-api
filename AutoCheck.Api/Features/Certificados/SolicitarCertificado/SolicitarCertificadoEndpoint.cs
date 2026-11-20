using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;

namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public static class SolicitarCertificadoEndpoint
{
    public static void MapSolicitarCertificado(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/inspecciones/{id:guid}/certificado", HandleAsync).RequireAuthorization().Interactive().WithTags("Certificados").Document<CertificateResponse>("SolicitarCertificado", "CU-06 / PA-06b,c", 202).Produces<CertificateResponse>();
    }
    private static async Task<Results<Ok<CertificateResponse>, Accepted<CertificateResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(Guid id, HttpContext h, Handler handler, CancellationToken ct)
    {
        var reply = await handler.Execute(new Command(id), h, ct);
        h.Response.Headers.Location = reply.Location;
        return reply.Status == 200 ? TypedResults.Ok(reply.Body!) : TypedResults.Accepted(reply.Location, reply.Body!);
    }
}
