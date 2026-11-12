using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SecurityFlowTests : DatabaseTest
{
    [IntegrationFact]
    public async Task PasswordRecoveryRevokesAllOwnSessionsAndPreservesOtherAccounts()
    {
        using var anonymous = App.CreateClient();
        using var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" });
        login.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        using var secondSession = App.CreateClient();
        secondSession.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await secondSession.GetAsync("/api/usuarios/me")).StatusCode);
        using var reset = await anonymous.PostAsJsonAsync("/api/auth/restablecimiento", new { token = "ciudadano@example.test", nuevaContrasena = "Nueva12345" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondSession.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Inspector.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.False(await WithDb(db => db.Sessions.AnyAsync(session => session.UserId == CitizenId && !session.Revoked)));
        var audit = await WithDb(db => db.Audit.SingleAsync(entry => entry.Operation == "RESTABLECIMIENTO_CONTRASENA"));
        Assert.Equal(CitizenId, audit.ActorId);
        Assert.Equal("SESIONES_REVOCADAS", audit.Outcome);
        Assert.DoesNotContain("Nueva12345", JsonSerializer.Serialize(audit));
        Assert.Equal("Nueva12345", App.Identity.Accounts["ciudadano@example.test"].Password);
    }

    [IntegrationFact]
    public async Task InvalidRecoveryCannotRevokeSessionsOrWriteAudit()
    {
        using var anonymous = App.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/api/auth/restablecimiento", new { token = "untrusted-recovery-token", nuevaContrasena = "Nueva12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        Assert.False(await WithDb(db => db.Audit.AnyAsync(entry => entry.Operation == "RESTABLECIMIENTO_CONTRASENA")));
        Assert.DoesNotContain("untrusted-recovery-token", await response.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task NullCollectionEntriesAreRejectedBeforeDatabaseSideEffects()
    {
        var schedules = await WithDb(db => db.Schedules.CountAsync());
        using var response = await Administrator.PutAsJsonAsync($"/api/centros/{CenterId}/horarios", new { horarios = new object?[] { null } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(schedules, await WithDb(db => db.Schedules.CountAsync()));
        using var defects = await Inspector.PutAsJsonAsync($"/api/inspecciones/{Guid.NewGuid()}/registro", new { versionEsperada = 0, defectos = new object?[] { null } });
        Assert.Equal(HttpStatusCode.BadRequest, defects.StatusCode);
        Assert.Equal(0, await WithDb(db => db.Inspections.CountAsync()));
    }

    [IntegrationFact]
    public async Task SensitiveResponsesAndErrorsCannotEnterSharedCaches()
    {
        using var anonymous = App.CreateClient();
        using var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" });
        using var profile = await Citizen.GetAsync("/api/usuarios/me");
        using var denied = await anonymous.GetAsync("/api/usuarios/me");
        foreach (var response in new[]
        {
            login,
            profile,
            denied
        }

        )
        {
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        }

        Assert.DoesNotContain("Abc12345", await login.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task UntrustedJwtCannotAcquireDatabasePrivileges()
    {
        using var anonymous = App.CreateClient();
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJBZG1pbmlzdHJhZG9yIn0.");
        using var response = await anonymous.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(3, await WithDb(db => db.Users.CountAsync()));
    }

    [IntegrationFact]
    public async Task MaximumDateProducesValidationErrorInsteadOfOverflow()
    {
        using var response = await Citizen.GetAsync($"/api/centros/{CenterId}/disponibilidad?fecha=9999-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Exception", await response.Content.ReadAsStringAsync());
    }
}
