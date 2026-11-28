// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.Common;

public static class CertificateQrUrl
{
    public static string Create(IConfiguration config, Certificate certificate)
    {
        var frontend = config["Certificates:FrontendBaseUrl"];
        var useFrontend = !string.IsNullOrWhiteSpace(frontend);
        var publicBase = useFrontend ? frontend : config["Certificates:PublicBaseUrl"];
        if (!Uri.TryCreate(publicBase, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || uri.Host.Length == 0 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw ApiContract.Unavailable();
        }

        var path = useFrontend ? $"/verificar/{certificate.Id}" : $"/api/certificados/{certificate.Id}/verificacion";
        return $"{uri.GetLeftPart(UriPartial.Path).TrimEnd('/')}{path}?hash={certificate.ManifestHash}";
    }
}
