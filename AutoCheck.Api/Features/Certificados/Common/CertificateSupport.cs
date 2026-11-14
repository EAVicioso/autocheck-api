using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Certificados.Common;

namespace AutoCheck.Api.Features.Certificados.Common;

public static class CertificateSupport
{
    public static CertificateResponse Response(Certificate c) => new(c.Id, c.State, c.Issued, c.ManifestHash, c.PdfHash);
}
