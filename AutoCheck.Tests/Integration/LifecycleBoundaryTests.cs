using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LifecycleBoundaryTests : DatabaseTest
{
    [IntegrationFact]
    public async Task EveryProtectedLifecycleRouteRejectsMissingAuthenticationBeforeBinding()
    {
        using var anonymous = App.CreateClient();
        var id = Guid.NewGuid();
        (HttpMethod Method, string Path)[] routes =
        [
            (HttpMethod.Post, "/api/auth/logout"), (HttpMethod.Post, "/api/auth/actividad"),
            (HttpMethod.Get, "/api/usuarios/me"), (HttpMethod.Patch, "/api/usuarios/me"),
            (HttpMethod.Get, "/api/usuarios"), (HttpMethod.Get, $"/api/usuarios/{id}"), (HttpMethod.Delete, $"/api/usuarios/{id}?version=1"),
            (HttpMethod.Get, "/api/roles"), (HttpMethod.Get, "/api/permisos"),
            (HttpMethod.Get, $"/api/usuarios/{id}/acceso"), (HttpMethod.Patch, $"/api/usuarios/{id}/acceso"),
            (HttpMethod.Get, "/api/catalogos/modelos"), (HttpMethod.Get, "/api/vehiculos"), (HttpMethod.Post, "/api/vehiculos"),
            (HttpMethod.Get, $"/api/vehiculos/{id}"), (HttpMethod.Patch, $"/api/vehiculos/{id}"), (HttpMethod.Delete, $"/api/vehiculos/{id}?version=1"),
            (HttpMethod.Get, $"/api/vehiculos/{id}/historial"), (HttpMethod.Post, $"/api/inspecciones/{id}/certificado"),
            (HttpMethod.Get, "/api/certificados"), (HttpMethod.Get, $"/api/certificados/{id}"), (HttpMethod.Get, $"/api/certificados/{id}/pdf"),
            (HttpMethod.Get, "/api/auditorias"), (HttpMethod.Get, $"/api/auditorias/{id}")
        ];
        foreach (var (method, path) in routes)
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await anonymous.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
    }
    [IntegrationFact]
    public async Task MissingResourcesUse404AndDeniedCataloguesUse403()
    {
        var id = Guid.NewGuid();
        foreach (var path in new[] { $"/api/usuarios/{id}", $"/api/usuarios/{id}/acceso", $"/api/vehiculos/{id}", $"/api/vehiculos/{id}/historial", $"/api/certificados/{id}", $"/api/certificados/{id}/pdf", $"/api/auditorias/{id}" })
        {
            using var response = await Administrator.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        foreach (var path in new[] { "/api/usuarios", "/api/roles", "/api/permisos", $"/api/usuarios/{CitizenId}", $"/api/usuarios/{CitizenId}/acceso", "/api/auditorias", $"/api/auditorias/{id}" })
        {
            using var response = await Citizen.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
    [IntegrationFact]
    public async Task MalformedPublicIdentityInputsFailBeforeProviderSideEffects()
    {
        using var anonymous = App.CreateClient();
        foreach (var operation in new[] { "registro", "login", "refresh", "recuperacion", "restablecimiento" })
        {
            using var response = await anonymous.PostAsJsonAsync("/api/auth/" + operation, new { });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        }
        Assert.Equal(3, App.Identity.Accounts.Count);
        Assert.Equal(0, App.Identity.ResetCalls);
    }
}
