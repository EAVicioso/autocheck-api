using System.Net;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace AutoCheck.Tests.Infrastructure;

public sealed class RecoveryEmailTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProviderRejectionCannotBeReportedAsSuccessfulEmailDelivery(HttpStatusCode status)
    {
        using var handler = new RejectedRecovery(status);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://example.test",
            ["Supabase:PublishableKey"] = "synthetic-publishable-key"
        }).Build();
        var provider = new SupabaseIdentity(http, config);
        var error = await Assert.ThrowsAsync<ApiException>(() => provider.Recover("owner@example.test", default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain("private-provider-response", error.Message);
        Assert.Equal("/auth/v1/recover", handler.Path);
    }

    private sealed class RejectedRecovery(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private-provider-response") });
        }
    }
}
