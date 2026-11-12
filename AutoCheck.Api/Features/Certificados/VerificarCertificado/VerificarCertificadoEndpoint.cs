using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;

namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public static class VerificarCertificadoEndpoint
{
    public static void MapVerificarCertificado(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/verificacion", async (Guid id, string hash, AutoCheckDb db, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().Where(x => x.Id == id && x.State == "EMITIDO").Select(x => new { x.Id, x.InspectionId, x.Issued, x.Manifest, x.ManifestHash }).SingleOrDefaultAsync(ct) ?? throw new ApiException(404, "CERTIFICADO_NO_ENCONTRADO", "Certificado no encontrado.");
            if (hash != c.ManifestHash || ApiContract.Hash(c.Manifest) != c.ManifestHash)
            {
                throw ApiContract.Conflict("INTEGRIDAD_NO_VALIDA", "Integridad no válida.");
            }

            var i = await db.Inspections.AsNoTracking().Where(x => x.Id == c.InspectionId).Select(x => new { x.CenterSnapshot, x.Result }).SingleAsync(ct);
            using var center = JsonDocument.Parse(i.CenterSnapshot);
            return new
            {
                certificadoId = c.Id,
                emisionUtc = c.Issued,
                centro = center.RootElement.GetProperty("nombre").GetString(),
                resultado = i.Result,
                integridad = "VALIDA"
            };
        }).AllowAnonymous().RequireRateLimiting("public").Document<VerificationResponse>("VerificarCertificado", "CU-07 / RF-CER-01 / PA-07a,b");
    }
}

public sealed record VerificationResponse(Guid CertificadoId, DateTimeOffset EmisionUtc, string Centro, string Resultado, string Integridad);
