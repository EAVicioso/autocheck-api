using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public static class VerificarCertificadoEndpoint
{
    public static void MapVerificarCertificado(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}/verificacion", HandleAsync).AllowAnonymous().RequireRateLimiting("public").Document<VerificationResponse>("VerificarCertificado", "RF-CER-01 / PA-07").ProducesProblem(StatusCodes.Status429TooManyRequests);
    private static async Task<Results<Ok<VerificationResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
