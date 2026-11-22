// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class QuerySecurityFlowTests : DatabaseTest
{
    private static readonly string[] GrantedPermissions = ["usuarios.gestionar", "auditoria.consultar"];
    private static readonly string[] RetainedPermissions = ["usuarios.gestionar"];
    [IntegrationFact]
    public async Task AggregateFilteredDeliveryNeverConsumesAnotherFixturesMessage()
    {
        var selected = Guid.NewGuid();
        var other = Guid.NewGuid();
        await WithDb(async db =>
        {
            foreach (var id in new[] { selected, other })
            {
                db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = id, Available = App.Clock.Now, Payload = JsonSerializer.Serialize(new { centroId = CenterId }) });
            }
            return await db.SaveChangesAsync();
        });
        using var scope = App.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<AutoCheck.Api.Infrastructure.OutboxDispatcher>();
        Assert.True(await dispatcher.DispatchOne(default, "DISPONIBILIDAD", selected));
        Assert.False(await dispatcher.DispatchOne(default, "DISPONIBILIDAD", selected));
        Assert.True(await WithDb(db => db.Outbox.AnyAsync(x => x.AggregateId == selected && x.Completed != null)));
        Assert.True(await WithDb(db => db.Outbox.AnyAsync(x => x.AggregateId == other && x.Completed == null && x.Attempts == 0)));
    }
    [IntegrationFact]
    public async Task ArchivedVehiclesAreOptInAndSortIsValidated()
    {
        using var deleted = await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var visible = await Citizen.GetAsync("/api/vehiculos?incluirArchivados=true&orden=matricula&direccion=desc");
        using var body = JsonDocument.Parse(await visible.Content.ReadAsStringAsync());
        Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        using var invalid = await Citizen.GetAsync("/api/vehiculos?orden=ownerId");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [IntegrationFact]
    public async Task AuditCursorDoesNotRepeatEventsAndRejectsWrongOrder()
    {
        await WithDb(async db =>
        {
            for (var n = 0; n < 4; n++)
            {
                db.Audit.Add(new() { At = App.Clock.Now.AddSeconds(n), Operation = "CURSOR", Entity = "synthetic", Outcome = "CONFIRMADO", Correlation = "cursor-test" });
            }
            return await db.SaveChangesAsync();
        });
        using var first = await Administrator.GetAsync("/api/auditorias?operacion=CURSOR&tamano=2");
        using var page = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var cursor = page.RootElement.GetProperty("siguienteCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        using var next = await Administrator.GetAsync("/api/auditorias?operacion=CURSOR&tamano=2&cursor=" + Uri.EscapeDataString(cursor));
        using var second = JsonDocument.Parse(await next.Content.ReadAsStringAsync());
        var ids = page.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.DoesNotContain(second.RootElement.GetProperty("items").EnumerateArray(), x => ids.Contains(x.GetProperty("id").GetGuid()));
        using var wrong = await Administrator.GetAsync("/api/auditorias?direccion=asc&cursor=" + Uri.EscapeDataString(cursor));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }
    [IntegrationFact]
    public async Task DeniedPermissionProducesImmutableSecurityEvent()
    {
        using var denied = await Citizen.GetAsync("/api/permisos");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(await WithDb(db => db.Audit.AnyAsync(x => x.ActorId == CitizenId && x.Operation == "SEGURIDAD_RECHAZADA" && x.Outcome == "403")));
    }
    [IntegrationFact]
    public async Task LogoutCanRetryItsOwnRevokedSession()
    {
        using var first = await Citizen.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var retry = await Citizen.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        using var protectedRoute = await Citizen.GetAsync("/api/usuarios/me");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedRoute.StatusCode);
        Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "LOGOUT")));
    }
    [IntegrationFact]
    public async Task PermissionsChangeOnNextRequestWithoutRefreshingJwt()
    {
        using var granted = await Administrator.PatchAsJsonAsync($"/api/usuarios/{CitizenId}/acceso", new { rol = "Administrador", permisos = GrantedPermissions, centroId = (Guid?)null, activo = true, version = 1 });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        using var allowed = await Citizen.GetAsync("/api/auditorias");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var removed = await Administrator.PatchAsJsonAsync($"/api/usuarios/{CitizenId}/acceso", new { rol = "Administrador", permisos = RetainedPermissions, centroId = (Guid?)null, activo = true, version = 2 });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var denied = await Citizen.GetAsync("/api/auditorias");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    [IntegrationFact]
    public async Task MassAssignmentCannotCreateAdministrativeAccess()
    {
        using var anonymous = App.CreateClient();
        var count = App.Identity.Accounts.Count;
        using var response = await anonymous.PostAsJsonAsync("/api/auth/registro", new { correo = "payload@example.test", contrasena = "Abc12345", nombre = "Test", rol = "Administrador" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(count, App.Identity.Accounts.Count);
    }
    [IntegrationFact]
    public async Task FunctionalRoleIsNotAnAcceptedTransportRole()
    {
        var auth = await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.AuthId).SingleAsync());
        var session = await WithDb(db => db.Sessions.Where(x => x.UserId == CitizenId).Select(x => x.Id).SingleAsync());
        using var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", FakeIdentity.Token(auth, session, "Administrador"));
        using var response = await client.GetAsync("/api/usuarios/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [IntegrationFact]
    public async Task ProviderOutageStillRevokesLocalSessionAndAllowsLogoutRetry()
    {
        App.Identity.LogoutUnavailable = true;
        using var first = await Citizen.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        using var denied = await Citizen.GetAsync("/api/usuarios/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        App.Identity.LogoutUnavailable = false;
        using var retry = await Citizen.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal(1, await WithDb(db => db.Audit.CountAsync(x => x.Operation == "LOGOUT")));
    }
    [IntegrationFact]
    public async Task IdentityRateLimitHasProblemDetailsAndRetryAfter()
    {
        using var anonymous = App.CreateClient();
        for (var n = 0; n < 30; n++)
        {
            using var rejected = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "invalid", contrasena = "" });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using var limited = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "invalid", contrasena = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var json = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
    }
    [IntegrationFact]
    public async Task OversizedJsonPreserves413AndNeverCallsIdentity()
    {
        using var anonymous = App.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/api/auth/registro", new { correo = "large@example.test", contrasena = "Abc12345", nombre = new string('x', 129 * 1024) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.False(App.Identity.Accounts.ContainsKey("large@example.test"));
    }
}
