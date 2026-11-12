// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoCheck.Tests;

public class PublicApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Database=autocheck_tests;Username=postgres", ["Workers:Enabled"] = "false", ["Testing:JwtKey"] = "autocheck-only-for-isolated-tests-0123456789" }));
}

public class ApiContractTests(PublicApiFactory application) : IClassFixture<PublicApiFactory>
{
    private readonly HttpClient client = application.CreateClient();

    [Fact]
    public async Task EveryProtectedOperationRejectsAnonymousRequestsBeforeBindingOrDatabaseAccess()
    {
        var routes = application.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(49, routes.Length);
        string[] publicNames = ["RegistrarCiudadano", "IniciarSesion", "RenovarSesion", "SolicitarRecuperacion", "RestablecerContrasena", "RecibirPagoSandbox", "VerificarCertificado"];
        var anonymous = routes.Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName).Order().ToArray();
        Assert.Equal(publicNames.Order(), anonymous);
        foreach (var endpoint in routes.Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            var path = endpoint.RoutePattern.RawText!;
            foreach (var parameter in endpoint.RoutePattern.Parameters)
            {
                var segment = "{" + parameter.Name + (parameter.ParameterPolicies.Count > 0 ? ":guid" : "") + "}";
                path = path.Replace(segment, Guid.Empty.ToString(), StringComparison.Ordinal);
            }
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                using var response = await client.SendAsync(request);
                Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }
    [Fact]
    public async Task HealthIsAnonymousAndPreservesExistingContract()
    {
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task PA11bDirectRESTCallsRequireAuthentication()
    {
        var response = await client.GetAsync("/api/usuarios");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiContainsAllSlicesAndNamedOperations()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        foreach (var route in new[]
        {
            "/api/auth/login",
            "/api/pagos/intentos",
            "/api/inspecciones/{id}/cierre",
            "/api/certificados/{id}/verificacion",
            "/api/asistencia",
            "/api/auditorias"
        }

        )
        {
            Assert.True(paths.TryGetProperty(route, out _), route);
        }
    }

    [Fact]
    public async Task ScalarServesDocumentationWithBearerAuthentication()
    {
        var response = await client.GetAsync("/scalar");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("AutoCheck API", html);
        Assert.Contains("openapi/v1.json", html);
        Assert.Contains("Bearer", html);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.Equal("bearer", json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(json.RootElement.GetProperty("paths").GetProperty("/api/usuarios").GetProperty("get").TryGetProperty("security", out _));
    }
}
