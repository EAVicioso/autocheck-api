// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public static class VerificarCertificadoEndpoint
{
    public static void MapVerificarCertificado(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/{id:guid}/verificacion", HandleAsync)
        .AllowAnonymous().RequireRateLimiting("public").Document<VerificationResponse>("VerificarCertificado", "RF-CER-01 / PA-07")
        .WithDescription("Verificación pública JSON de los QR anteriores y de la vista web /verificar/{id}, que envía el mismo id y hash.")
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    private static async Task<Results<Ok<VerificationResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
