using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoCheck.Api.Features.Certificados;
using AutoCheck.Api.Features.Pagos;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Security.Cryptography.X509Certificates;
using AutoCheck.Tests.Features.Certificados;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public class BusinessFlowTests : DatabaseTest
{
    [IntegrationFact]
    public void PA10_RestAssured_checks_the_real_HTTP_contract()
    {
        RestAssured.Dsl.Given().Header("Authorization", Citizen.DefaultRequestHeaders.Authorization!.ToString())
            .When().Get(new Uri(Citizen.BaseAddress!, "/api/usuarios/me").ToString()).Then().StatusCode(200);
        RestAssured.Dsl.Given().When().Get(new Uri(Citizen.BaseAddress!, "/api/usuarios/me").ToString()).Then().StatusCode(401);
    }
    private async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    private Task<HttpResponseMessage> Start(string? key = null) => Send(Citizen, HttpMethod.Post, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, Starts, DemoraSegundos: 600), key ?? Guid.NewGuid().ToString());
    private async Task<JsonElement> Webhook(Guid attempt, string outcome = "APROBADO", string? eventId = null)
    {
        var body = JsonSerializer.Serialize(new SandboxEvent(eventId ?? Guid.NewGuid().ToString(), attempt, outcome), ApiReply.JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pagos/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Sandbox-Signature", Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("sandbox-for-isolated-tests-only-0123456789"), Encoding.UTF8.GetBytes(body))));
        return await Json(await Citizen.SendAsync(request));
    }

    [IntegrationFact]
    public async Task PA03a_Concurrent_requests_yield_one_hold_and_one_conflict()
    {
        var responses = await Task.WhenAll(Start(), Start());
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("BLOQUE_NO_DISPONIBLE", await conflict.Content.ReadAsStringAsync());
        Assert.Equal(1, await WithDb(db => db.Payments.CountAsync()));
    }
    [IntegrationFact]
    public async Task PA03d_Idempotency_reuses_expiration_and_rejects_changed_payload()
    {
        var first = await Json(await Start("same-request"), HttpStatusCode.Created); App.Clock.Advance(TimeSpan.FromSeconds(50));
        var again = await Json(await Start("same-request"), HttpStatusCode.Created); Assert.Equal(first.GetRawText(), again.GetRawText());
        var changed = await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, Starts.AddMinutes(30)), "same-request");
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }
    [IntegrationFact]
    public async Task PA03b_Payment_at_299_seconds_confirms_once()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); var id = hold.GetProperty("intentoId").GetGuid(); App.Clock.Advance(TimeSpan.FromSeconds(299));
        var result = await Webhook(id, eventId: "same-event"); var replay = await Webhook(id, eventId: "same-event");
        Assert.Equal(result.GetRawText(), replay.GetRawText()); Assert.Equal(1, await WithDb(db => db.Appointments.CountAsync()));
    }
    [IntegrationFact]
    public async Task PA03c_PA04c_Expired_hold_can_be_reacquired_and_late_payment_never_steals_it()
    {
        var old = await Json(await Start(), HttpStatusCode.Created); App.Clock.Advance(TimeSpan.FromSeconds(300));
        var current = await Json(await Start(), HttpStatusCode.Created);
        var late = await Webhook(old.GetProperty("intentoId").GetGuid(), eventId: "late");
        Assert.Equal("REVERTIDO", late.GetProperty("estado").GetString());
        var repeat = await Webhook(old.GetProperty("intentoId").GetGuid(), eventId: "late"); Assert.Equal(late.GetRawText(), repeat.GetRawText());
        Assert.Equal(current.GetProperty("intentoId").GetGuid(), await WithDb(db => db.Blocks.Select(x => x.AttemptId).SingleAsync()));
        Assert.Equal(0, await WithDb(db => db.Appointments.CountAsync()));
    }
    [IntegrationFact]
    public async Task PA04b_Rejected_payment_releases_capacity()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); await Webhook(hold.GetProperty("intentoId").GetGuid(), "RECHAZADO");
        Assert.Equal(0, await WithDb(db => db.Blocks.CountAsync())); await Json(await Start(), HttpStatusCode.Created);
    }
    [IntegrationFact]
    public async Task PA04d_Cancellation_reverses_once()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); var paid = await Webhook(hold.GetProperty("intentoId").GetGuid()); var id = paid.GetProperty("citaId").GetGuid();
        var first = await Json(await Send(Citizen, HttpMethod.Post, $"/api/citas/{id}/cancelacion", new { motivo = "Prueba" }, "cancel"));
        var second = await Json(await Send(Citizen, HttpMethod.Post, $"/api/citas/{id}/cancelacion", new { motivo = "Prueba" }, "cancel2"));
        Assert.Equal(first.GetProperty("reversionId").GetGuid(), second.GetProperty("reversionId").GetGuid()); Assert.Equal(0, await WithDb(db => db.Blocks.CountAsync()));
    }
    [IntegrationFact]
    public async Task PA12b_Active_hold_prevents_ramp_deactivation()
    {
        await Json(await Start(), HttpStatusCode.Created);
        var response = await Send(Administrator, HttpMethod.Patch, $"/api/centros/{CenterId}/rampas/{RampId}", new { nombre = "R1", activa = false });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    [IntegrationFact]
    public async Task PA05_PA06_PA07_Full_flow_closes_immutably_and_downloads_identical_signed_bytes()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); var payment = await Webhook(hold.GetProperty("intentoId").GetGuid()); var appointment = payment.GetProperty("citaId").GetGuid();
        await Json(await Send(Administrator, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }));
        var started = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, "start"), HttpStatusCode.Created); var inspection = started.GetProperty("id").GetGuid();
        var incomplete = await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{inspection}/cierre", new { versionEsperada = 0 }, "incomplete"); Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);
        await Json(await Send(Inspector, HttpMethod.Put, $"/api/inspecciones/{inspection}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }));
        var closed = await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{inspection}/cierre", new { versionEsperada = 1 }, "close")); Assert.Equal("Aprobado", closed.GetProperty("resultado").GetString());
        var replay = await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{inspection}/cierre", new { versionEsperada = 1 }, "close")); Assert.Equal(closed.GetRawText(), replay.GetRawText());
        var edit = await Send(Inspector, HttpMethod.Put, $"/api/inspecciones/{inspection}/registro", new { versionEsperada = 2, defectos = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AutoCheckDb>(); await using var tx = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<CertificateIssuer>().Complete(inspection, default); await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        var certificate = await WithDb(db => db.Certificates.AsNoTracking().SingleAsync());
        var bytes1 = await Citizen.GetByteArrayAsync($"/api/certificados/{certificate.Id}/pdf"); var bytes2 = await Citizen.GetByteArrayAsync($"/api/certificados/{certificate.Id}/pdf"); Assert.Equal(bytes1, bytes2);
        Assert.Equal(certificate.PdfHash, Convert.ToHexStringLower(SHA256.HashData(bytes1))); Assert.Contains("/ByteRange", Encoding.Latin1.GetString(bytes1));
        using var trust = X509CertificateLoader.LoadPkcs12FromFile(App.PfxPath, null);
        Assert.True(OfflinePdfVerifier.Verify(bytes1, trust));
        var tampered = bytes1.ToArray(); tampered[20] ^= 1; Assert.False(OfflinePdfVerifier.Verify(tampered, trust));
        if (Environment.GetEnvironmentVariable("AUTOCHECK_CAPTURE_PDF") is { Length: > 0 } capture)
        {
            Directory.CreateDirectory(capture); await File.WriteAllBytesAsync(Path.Combine(capture, "certificado-prueba.pdf"), bytes1);
            await File.WriteAllTextAsync(Path.Combine(capture, "emisor-publico.pem"), trust.ExportCertificatePem());
        }
        using var anonymous = App.CreateClient(); var publicData = await Json(await anonymous.GetAsync($"/api/certificados/{certificate.Id}/verificacion?hash={certificate.ManifestHash}"));
        Assert.Equal(5, publicData.EnumerateObject().Count()); Assert.False(publicData.TryGetProperty("inspeccionId", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.GetAsync($"/api/certificados/{certificate.Id}/verificacion?hash={new string('0', 64)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Inspector.GetAsync($"/api/certificados/{certificate.Id}/pdf")).StatusCode);
        var history = await Json(await Citizen.GetAsync($"/api/vehiculos/{VehicleId}/historial")); Assert.Single(history.GetProperty("items").EnumerateArray());
        await Assert.ThrowsAsync<PostgresException>(() => WithDb(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE autocheck.\"Inspection\" SET \"Result\"='Rechazado' WHERE \"Id\"={inspection}")));
    }
    [IntegrationFact]
    public async Task PA02b_Exact_inactivity_boundary_is_enforced_on_REST()
    {
        App.Clock.Advance(TimeSpan.FromSeconds(3599)); Assert.Equal(HttpStatusCode.OK, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
        App.Clock.Advance(TimeSpan.FromSeconds(1)); Assert.Equal(HttpStatusCode.Unauthorized, (await Citizen.GetAsync("/api/usuarios/me")).StatusCode);
    }
    [IntegrationFact]
    public async Task PA02c_Five_failures_block_without_extending_and_unlock_at_15_minutes()
    {
        using var anonymous = App.CreateClient();
        for (var n = 0; n < 5; n++) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "incorrecta" })).StatusCode);
        var until = await WithDb(db => db.AccountLocks.Select(x => x.LockedUntil).SingleAsync());
        App.Clock.Advance(TimeSpan.FromSeconds(899)); Assert.Equal(HttpStatusCode.TooManyRequests, (await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" })).StatusCode);
        Assert.Equal(until, await WithDb(db => db.AccountLocks.Select(x => x.LockedUntil).SingleAsync()));
        App.Clock.Advance(TimeSpan.FromSeconds(1)); await Json(await anonymous.PostAsJsonAsync("/api/auth/login", new { correo = "ciudadano@example.test", contrasena = "Abc12345" }));
        Assert.Equal(0, await WithDb(db => db.AccountLocks.Select(x => x.Failures).SingleAsync()));
    }
    [IntegrationFact]
    public async Task PA11_PA13_Permissions_are_server_controlled_and_audit_is_append_only()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Citizen.GetAsync("/api/auditorias")).StatusCode); // JWT includes a spoofed admin role.
        await Json(await Start(), HttpStatusCode.Created); var events = await Json(await Administrator.GetAsync("/api/auditorias")); Assert.NotEmpty(events.GetProperty("items").EnumerateArray());
        await Assert.ThrowsAsync<PostgresException>(() => WithDb(db => db.Database.ExecuteSqlRawAsync("UPDATE autocheck.\"AuditEvent\" SET \"Outcome\"='CHANGED'")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Citizen.PatchAsJsonAsync($"/api/usuarios/{InspectorId}/acceso", new { rol = "Administrador", permisos = new[] { "usuarios.gestionar" }, activo = true })).StatusCode);
    }
    [IntegrationFact]
    public async Task PA09_Invalid_and_unavailable_assistance_preserves_exact_contract_and_deadline()
    {
        var invalid = await Json(await Citizen.PostAsJsonAsync("/api/asistencia", new { consulta = " " }), HttpStatusCode.BadRequest); Assert.Equal(3, invalid.EnumerateObject().Count()); Assert.Equal(0, App.Assistance.Calls);
        foreach (var mode in new[] { "invalid", "error", "timeout" })
        {
            App.Assistance.Mode = mode; var watch = Stopwatch.StartNew();
            var result = await Json(await Citizen.PostAsJsonAsync("/api/asistencia", new { consulta = "¿Cómo reservo una cita?" }), HttpStatusCode.ServiceUnavailable);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3)); Assert.Equal(3, result.EnumerateObject().Count()); Assert.Equal("SERVICIO_NO_DISPONIBLE", result.GetProperty("codigo").GetString());
        }
    }

    [IntegrationFact]
    public async Task PA05e_Induced_commit_failure_leaves_no_partial_close_or_certificate()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); var paid = await Webhook(hold.GetProperty("intentoId").GetGuid()); var appointment = paid.GetProperty("citaId").GetGuid();
        await Json(await Send(Administrator, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }));
        var started = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, "start"), HttpStatusCode.Created); var id = started.GetProperty("id").GetGuid();
        await Json(await Send(Inspector, HttpMethod.Put, $"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }));
        await WithDb(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION autocheck.fail_close() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
            IF NEW."Entity" LIKE '%/cierre' THEN RAISE EXCEPTION 'induced failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_close BEFORE INSERT ON autocheck."AuditEvent" FOR EACH ROW EXECUTE FUNCTION autocheck.fail_close();
            """));
        var result = await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "failed-close"); Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("ABIERTA", await WithDb(db => db.Inspections.Select(x => x.State).SingleAsync())); Assert.Equal(0, await WithDb(db => db.Certificates.CountAsync()));
        Assert.Equal(0, await WithDb(db => db.Outbox.CountAsync(x => x.Kind == "COPIA_MONGO" || x.Kind == "CERTIFICADO")));
    }

    [IntegrationFact]
    public async Task PA06c_Outbox_retries_preserve_certificate_identity_and_do_not_depend_on_Mongo()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created); var paid = await Webhook(hold.GetProperty("intentoId").GetGuid()); var appointment = paid.GetProperty("citaId").GetGuid();
        await Json(await Send(Administrator, HttpMethod.Put, $"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }));
        var started = await Json(await Send(Inspector, HttpMethod.Post, "/api/inspecciones", new { citaId = appointment }, "start"), HttpStatusCode.Created); var id = started.GetProperty("id").GetGuid();
        await Json(await Send(Inspector, HttpMethod.Put, $"/api/inspecciones/{id}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }));
        await Json(await Send(Inspector, HttpMethod.Post, $"/api/inspecciones/{id}/cierre", new { versionEsperada = 1 }, "close"));
        var certificateId = await WithDb(db => db.Certificates.Select(x => x.Id).SingleAsync()); App.Archive.Fail = true;
        for (var n = 0; n < 10; n++) { using var scope = App.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default); }
        var state = await WithDb(db => db.Certificates.Select(x => x.State).SingleAsync()); Assert.Equal("EMITIDO", state);
        Assert.True(await WithDb(db => db.Outbox.AnyAsync(x => x.Kind == "COPIA_MONGO" && x.Attempts > 0 && x.Completed == null)));
        App.Archive.Fail = false; App.Clock.Advance(TimeSpan.FromSeconds(5));
        for (var n = 0; n < 5; n++) { using var scope = App.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default); }
        Assert.Equal(certificateId, await WithDb(db => db.Certificates.Select(x => x.Id).SingleAsync())); Assert.Single(App.Archive.Documents);
        Assert.True(await WithDb(db => db.Outbox.AnyAsync(x => x.Kind == "COPIA_MONGO" && x.Completed != null)));
    }
}
