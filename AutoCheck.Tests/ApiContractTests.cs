// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Chatbot.Common;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AutoCheck.Tests;

public class PublicApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Database=autocheck_tests;Username=postgres", ["Workers:Enabled"] = "false", ["Testing:JwtKey"] = "autocheck-only-for-isolated-tests-0123456789" }));
}

public class ApiContractTests(PublicApiFactory application) : IClassFixture<PublicApiFactory>
{
    private readonly HttpClient client = application.CreateClient();

    [Fact]
    public async Task PA09ColdJwksWaitReturnsExactContingencyWithinThreeSeconds()
    {
        const string issuer = "https://unconfigured.invalid/auth/v1";
        var document = new SlowJwks();
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(issuer + "/.well-known/jwks.json", new SupabaseJwks(issuer), document);
        await using var server = application.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.ConfigurationManager = manager)));
        server.UseKestrel(0);
        using var http = server.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/asistencia")
        {
            Content = JsonContent.Create(new { consulta = "¿Cómo reservo una cita?" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic.jwt.placeholder");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var pending = http.SendAsync(request, deadline.Token);
            await document.Entered.Task.WaitAsync(deadline.Token);
            using var response = await pending;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Contingency took {stopwatch.Elapsed}.");
            Assert.Equal(3, json.RootElement.EnumerateObject().Count());
            Assert.Equal(AssistanceSupport.Contingency, json.RootElement.GetProperty("respuesta").GetString());
            Assert.Equal("NO_DETERMINADA", json.RootElement.GetProperty("categoria").GetString());
            Assert.Equal("SERVICIO_NO_DISPONIBLE", json.RootElement.GetProperty("codigo").GetString());
        }
        finally
        {
            document.Release.TrySetResult("""{"keys":[]}""");
            await manager.GetConfigurationAsync(deadline.Token);
        }
    }

    private sealed class SlowJwks : IDocumentRetriever
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            Entered.TrySetResult();
            return Release.Task.WaitAsync(cancel);
        }
    }

    [Fact]
    public async Task EveryProtectedOperationRejectsAnonymousRequestsBeforeBindingOrDatabaseAccess()
    {
        var routes = application.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(57, routes.Length);
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
        Assert.True(paths.GetProperty("/api/auth/login").GetProperty("post").GetProperty("responses").TryGetProperty("429", out _));
        Assert.True(paths.GetProperty("/api/certificados/{id}/verificacion").GetProperty("get").GetProperty("responses").TryGetProperty("429", out _));
        Assert.True(paths.GetProperty("/api/usuarios/{id}").GetProperty("delete").GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(paths.GetProperty("/api/vehiculos/{id}").GetProperty("delete").GetProperty("responses").TryGetProperty("503", out _));
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
