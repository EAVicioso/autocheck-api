using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AutoCheck.Api.Infrastructure.Security;
using AutoCheck.Tests.Features.Certificados;
using Microsoft.EntityFrameworkCore;
namespace AutoCheck.Tests.Integration;

[Collection("Supabase hosted")]
[Trait("Category", "SupabaseE2E")]
public sealed class SupabaseFullLifecycleTests
{
    [SupabaseFact]
    public async Task SixModulesUseRealAuthHttpPersistenceAndSignedPdf()
    {
        await using var scenario = new SupabaseScenario();
        await scenario.Preflight();
        var admin = await scenario.BootstrapAdministrator();
        var citizen = await scenario.RegisterCitizen('c');
        var inspector = await scenario.GenerateConfirmedUser('i');
        await CheckUsersAndPermissions(scenario, admin, citizen);
        var ramp = await scenario.CreateCenter(admin);
        await AssignInspector(scenario, admin, inspector);
        var vehicle = await CheckVehicleCrud(scenario, citizen);
        var appointment = await scenario.PaidAppointment(citizen, vehicle, ramp);
        var inspection = await CloseInspection(admin, inspector, appointment);
        await CheckCertificates(scenario, citizen, vehicle, inspection);
        await scenario.VerifyAtlasProjection(inspection);
        await CheckAudit(admin, citizen.Id);
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Delete, $"/api/vehiculos/{vehicle}?version=2", status: HttpStatusCode.NoContent);
        await CheckIdentity(scenario, citizen);
        await ArchiveResources(admin, citizen.Id, inspector.Id);
        Assert.True(await scenario.Db(db => db.Audit.AnyAsync(x => x.ActorId == citizen.Id && x.Operation == "VEHICULO_ACTUALIZADO")));
        Assert.True(await scenario.Db(db => db.Users.AnyAsync(x => x.Id == citizen.Id && x.ArchivedAt != null)));
    }
    private static async Task CheckUsersAndPermissions(SupabaseScenario scenario, FixtureUser admin, FixtureUser citizen)
    {
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, "/api/roles");
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, "/api/permisos");
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, "/api/usuarios?rol=Ciudadano&activo=true&orden=nombre&direccion=asc");
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, $"/api/usuarios/{citizen.Id}");
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, $"/api/usuarios/{citizen.Id}/acceso");
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/usuarios", status: HttpStatusCode.Forbidden);
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Patch, "/api/usuarios/me", new { nombre = "E2E " + scenario.RunId, celular = "", version = 1 });
    }
    private static async Task AssignInspector(SupabaseScenario scenario, FixtureUser admin, FixtureUser inspector)
    {
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Patch, $"/api/usuarios/{inspector.Id}/acceso", new { rol = Roles.Inspector, permisos = Array.Empty<string>(), centroId = scenario.CenterId, activo = true, version = 1 });
    }
    private static async Task<Guid> CheckVehicleCrud(SupabaseScenario scenario, FixtureUser citizen)
    {
        var models = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/catalogos/modelos");
        var colors = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/catalogos/colores");
        var model = models.GetProperty("items")[0].GetProperty("id").GetGuid();
        var color = colors.GetProperty("items")[0].GetProperty("id").GetGuid();
        var unique = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var created = await SupabaseScenario.Send(citizen.Client, HttpMethod.Post, "/api/vehiculos", new { matricula = "E2E" + scenario.RunId, vin = unique[..17], anio = 2020, modeloId = model, colorId = color }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/vehiculos?orden=matricula&direccion=desc");
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, $"/api/vehiculos/{id}");
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Patch, $"/api/vehiculos/{id}", new { matricula = "E2E" + scenario.RunId, vin = unique[..17], anio = 2021, modeloId = model, colorId = color, version = 1 });
        return id;
    }
    private static async Task<Guid> CloseInspection(FixtureUser admin, FixtureUser inspector, Guid appointment)
    {
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = inspector.Id });
        var started = await SupabaseScenario.Send(inspector.Client, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, HttpStatusCode.Created, true);
        var id = started.GetProperty("id").GetGuid();
        _ = await SupabaseScenario.Send(inspector.Client, HttpMethod.Put, $"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() });
        var closed = await SupabaseScenario.Send(inspector.Client, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, idempotent: true);
        Assert.Equal("Aprobado", closed.GetProperty("resultado").GetString());
        return id;
    }
    private static async Task CheckCertificates(SupabaseScenario scenario, FixtureUser citizen, Guid vehicle, Guid inspection)
    {
        var pending = await SupabaseScenario.Send(citizen.Client, HttpMethod.Post, $"/api/inspecciones/{inspection}/certificado", status: HttpStatusCode.Accepted, idempotent: true, alternateStatus: HttpStatusCode.OK);
        var id = pending.GetProperty("id").GetGuid();
        await scenario.DispatchCertificate(inspection);
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/certificados?estado=EMITIDO");
        var detail = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, $"/api/certificados/{id}");
        Assert.Equal("EMITIDO", detail.GetProperty("estado").GetString());
        using var response = await citizen.Client.GetAsync($"/api/certificados/{id}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(detail.GetProperty("hashPdf").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.True(OfflinePdfVerifier.Verify(bytes, scenario.SigningCertificate));
        using var repeat = await citizen.Client.GetAsync($"/api/certificados/{id}/pdf");
        Assert.Equal(bytes, await repeat.Content.ReadAsByteArrayAsync());
        using var publicClient = scenario.Client();
        var verified = await SupabaseScenario.Send(publicClient, HttpMethod.Get, $"/api/certificados/{id}/verificacion?hash={detail.GetProperty("hashManifiesto").GetString()}");
        Assert.Equal(5, verified.EnumerateObject().Count());
        Assert.Equal("VALIDA", verified.GetProperty("integridad").GetString());
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, $"/api/vehiculos/{vehicle}/historial");
    }
    private static async Task CheckAudit(FixtureUser admin, Guid actor)
    {
        var page = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, $"/api/auditorias?actorId={actor}&tamano=2");
        var id = page.GetProperty("items")[0].GetProperty("id").GetGuid();
        var detail = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, $"/api/auditorias/{id}");
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("correlacion").GetString()));
        if (page.GetProperty("siguienteCursor").GetString() is { } cursor)
        {
            _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Get, $"/api/auditorias?actorId={actor}&tamano=2&cursor=" + Uri.EscapeDataString(cursor));
        }
    }
    private static async Task CheckIdentity(SupabaseScenario scenario, FixtureUser citizen)
    {
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Post, "/api/auth/actividad", status: HttpStatusCode.NoContent);
        using var anonymous = scenario.Client();
        var refreshed = await SupabaseScenario.Send(anonymous, HttpMethod.Post, "/api/auth/refresh", new { refreshToken = citizen.RefreshToken });
        citizen.Client.DefaultRequestHeaders.Authorization = new("Bearer", refreshed.GetProperty("accessToken").GetString());
        _ = await SupabaseScenario.Send(anonymous, HttpMethod.Post, "/api/auth/recuperacion", new { correo = citizen.Email }, HttpStatusCode.Accepted);
        var reset = await scenario.ResetPassword(citizen);
        _ = await SupabaseScenario.Send(citizen.Client, HttpMethod.Get, "/api/usuarios/me", status: HttpStatusCode.Unauthorized);
        _ = await SupabaseScenario.Send(reset.Client, HttpMethod.Post, "/api/auth/logout", status: HttpStatusCode.NoContent);
        _ = await SupabaseScenario.Send(anonymous, HttpMethod.Post, "/api/auth/refresh", new { refreshToken = reset.RefreshToken }, HttpStatusCode.Unauthorized);
    }
    private static async Task ArchiveResources(FixtureUser admin, Guid citizen, Guid inspector)
    {
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Delete, $"/api/usuarios/{citizen}?version=2", status: HttpStatusCode.NoContent);
        _ = await SupabaseScenario.Send(admin.Client, HttpMethod.Delete, $"/api/usuarios/{inspector}?version=2", status: HttpStatusCode.NoContent);
    }
}
