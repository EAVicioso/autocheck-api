using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AutoCheck.Tests;
public class PublicApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder b) => b.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
    { ["Workers:Enabled"] = "false", ["Testing:JwtKey"] = "autocheck-only-for-isolated-tests-0123456789" }));
}
public class ApiContractTests : IClassFixture<PublicApiFactory>
{
    private readonly HttpClient client;
    public ApiContractTests(PublicApiFactory factory) => client = factory.CreateClient();
    [Fact] public async Task Health_is_anonymous_and_preserves_existing_contract()
    {
        var response = await client.GetAsync("/health"); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString()); Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
    }
    [Fact] public async Task PA11b_Direct_REST_calls_require_authentication()
    {
        var response = await client.GetAsync("/api/usuarios"); Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact] public async Task OpenApi_contains_all_slices_and_named_operations()
    {
        var response = await client.GetAsync("/openapi/v1.json"); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var paths = json.RootElement.GetProperty("paths");
        foreach (var route in new[] { "/api/auth/login", "/api/pagos/intentos", "/api/inspecciones/{id}/cierre", "/api/certificados/{id}/verificacion", "/api/asistencia", "/api/auditorias" }) Assert.True(paths.TryGetProperty(route, out _), route);
    }
}
