using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;

namespace AutoCheck.Api.Features.Certificados.DescargarCertificado;

public static class DescargarCertificadoEndpoint
{
    public static void MapDescargarCertificado(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/pdf", async (Guid id, AutoCheckDb db, AppActor actor, HttpContext h, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (c.OwnerId != actor.Id)
            {
                AppActor.Deny();
            }

            if (c.State != "EMITIDO" || c.Pdf is null)
            {
                throw ApiContract.Conflict("CERTIFICADO_PENDIENTE", "El certificado aún no está disponible.");
            }

            h.Response.Headers.CacheControl = "private, no-store";
            return Results.File(c.Pdf, "application/pdf", $"AutoCheck-{c.Id}.pdf", enableRangeProcessing: false);
        }).RequireAuthorization().WithName("DescargarCertificado").WithSummary("CU-06 / PA-06a,c").Produces(200, contentType: "application/pdf").ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }
}
