using System.Net;
using System.Text;
using System.Text.Json;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace AutoCheck.Tests.Infrastructure;

public sealed class PasswordResetProviderTests
{
    [Fact]
    public async Task IdentityPrevalidationUsesAnAuthenticatedGetWithoutPasswordMutation()
    {
        var authId = Guid.NewGuid();
        using var handler = new AuthResponse(HttpStatusCode.OK, authId);
        using var http = new HttpClient(handler);
        var provider = Create(http);
        Assert.Equal(authId, await provider.ValidateRecovery("synthetic-recovery-token", default));
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/auth/v1/user", handler.Path);
        Assert.False(handler.HadBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task DefinitiveRejectionsAreDistinguishedFromUncertainDelivery(HttpStatusCode status)
    {
        using var handler = new AuthResponse(status, Guid.NewGuid());
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<RecoveryRejectedException>(() => Create(http).Reset("synthetic-recovery-token", "Synthetic12345", default));
        Assert.Equal(HttpMethod.Put, handler.Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task TransientErrorsCannotCompensateTheRevocationBarrier(HttpStatusCode status)
    {
        using var handler = new AuthResponse(status, Guid.NewGuid());
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiException>(() => Create(http).Reset("synthetic-recovery-token", "Synthetic12345", default));
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain("private-provider-response", error.Message);
    }

    private static SupabaseIdentity Create(HttpClient http) => new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Supabase:Url"] = "https://example.test",
        ["Supabase:PublishableKey"] = "synthetic-publishable-key"
    }).Build());

    private sealed class AuthResponse(HttpStatusCode status, Guid authId) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public bool HadBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            HadBody = request.Content is not null;
            var body = status == HttpStatusCode.OK ? JsonSerializer.Serialize(new { id = authId }) : "private-provider-response";
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
