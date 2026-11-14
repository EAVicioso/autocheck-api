using AutoCheck.Api.Features.Certificados.DescargarCertificado;
using AutoCheck.Api.Features.Certificados.ObtenerCertificado;
using AutoCheck.Api.Features.Certificados.SolicitarCertificado;
using AutoCheck.Api.Features.Certificados.VerificarCertificado;

namespace AutoCheck.Api.Features.Certificados;

public static class CertificateEndpoints
{
    public static void MapCertificates(this IEndpointRouteBuilder routes)
    {
        routes.MapSolicitarCertificado();
        var g = routes.MapGroup("/api/certificados").WithTags("Certificados");
        g.MapObtenerCertificado();
        g.MapDescargarCertificado();
        g.MapVerificarCertificado();
    }
}
