// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

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
    public async Task RecoveryExchangesEmailProofWithoutPasswordMutation()
    {
        var authId = Guid.NewGuid();
        using var handler = new AuthResponse(HttpStatusCode.OK, authId);
        using var http = new HttpClient(handler);
        var provider = Create(http);
        var proof = await provider.ValidateRecovery("synthetic-recovery-token", default);
        Assert.Equal(authId, proof.AuthId);
        Assert.Equal("synthetic-provider-session", proof.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/auth/v1/verify", handler.Path);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("recovery", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("synthetic-recovery-token", body.RootElement.GetProperty("token_hash").GetString());
        Assert.False(body.RootElement.TryGetProperty("password", out _));
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

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.RequestTimeout, 503)]
    [InlineData(HttpStatusCode.TooManyRequests, 503)]
    [InlineData(HttpStatusCode.BadGateway, 503)]
    public async Task RegistrationDistinguishesInvalidInputFromProviderOutage(HttpStatusCode status, int expectedStatus)
    {
        using var handler = new AuthResponse(status, Guid.NewGuid());
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiException>(() => Create(http).Register("owner@example.test", "Synthetic12345", default));
        Assert.Equal(expectedStatus, error.Status);
        Assert.DoesNotContain("private-provider-response", error.Message);
    }

    [Fact]
    public async Task PA01bObfuscatedSignupDoesNotReturnARegistryIdentity()
    {
        var payload = JsonSerializer.Serialize(new { id = Guid.NewGuid(), identities = Array.Empty<object>() });
        using var handler = new AuthResponse(HttpStatusCode.OK, Guid.NewGuid(), payload);
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiException>(
            () => Create(http).Register("owner@example.test", "Synthetic12345", default));
        Assert.Equal(409, error.Status);
        Assert.Equal("CORREO_EXISTENTE", error.Code);
    }

    private static SupabaseIdentity Create(HttpClient http) => new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Supabase:Url"] = "https://example.test",
        ["Supabase:PublishableKey"] = "synthetic-publishable-key"
    }).Build());

    private sealed class AuthResponse(HttpStatusCode status, Guid authId, string? payload = null) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var body = payload ?? (status == HttpStatusCode.OK
                ? JsonSerializer.Serialize(new { user = new { id = authId }, access_token = "synthetic-provider-session" })
                : "private-provider-response");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
