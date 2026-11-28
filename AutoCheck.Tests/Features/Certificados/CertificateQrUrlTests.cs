// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Certificados.Common;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace AutoCheck.Tests.Features.Certificados;

public class CertificateQrUrlTests
{
    private readonly Certificate certificate = new() { Id = Guid.NewGuid(), ManifestHash = new string('a', 64) };

    [Fact]
    public void PA07ConfiguredFrontendProvidesPublicPageWithCertificateIdAndHash()
    {
        var config = Config("https://web.example.test/", api: null);
        Assert.Equal($"https://web.example.test/verificar/{certificate.Id}?hash={certificate.ManifestHash}",
            CertificateQrUrl.Create(config, certificate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void PA07MissingFrontendUsesExistingJsonEndpoint(string? frontend)
    {
        var config = Config(frontend, "https://api.example.test/");
        Assert.Equal($"https://api.example.test/api/certificados/{certificate.Id}/verificacion?hash={certificate.ManifestHash}",
            CertificateQrUrl.Create(config, certificate));
    }

    [Theory]
    [InlineData("/verificar")]
    [InlineData("http://web.example.test")]
    [InlineData("https://synthetic:credential@web.example.test")]
    [InlineData("https://web.example.test?key=synthetic")]
    [InlineData("https://web.example.test#fragment")]
    public void PA07InvalidFrontendSettingFailsWithoutPublishingAUrl(string frontend)
    {
        var error = Assert.Throws<ApiException>(() => CertificateQrUrl.Create(Config(frontend, "https://api.example.test"), certificate));
        Assert.Equal(503, error.Status);
    }

    private static IConfiguration Config(string? frontend, string? api) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Certificates:FrontendBaseUrl"] = frontend, ["Certificates:PublicBaseUrl"] = api }).Build();
}
