using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;

namespace AutoCheck.Api.Features.Certificados.ObtenerCertificado;

public static class ObtenerCertificadoEndpoint
{
    public static void MapObtenerCertificado(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.OwnerId, x.State, x.Issued, x.ManifestHash, x.PdfHash }).SingleOrDefaultAsync(ct) ?? throw ApiContract.Missing();
            if (c.OwnerId != actor.Id)
            {
                AppActor.Deny();
            }

            return new CertificateResponse(c.Id, c.State, c.Issued, c.ManifestHash, c.PdfHash);
        }).RequireAuthorization().Document<CertificateResponse>("ObtenerCertificado", "CU-06");
    }
}
