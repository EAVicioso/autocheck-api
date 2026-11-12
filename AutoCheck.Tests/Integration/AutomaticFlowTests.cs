// AutoCheck / INTEC, SRS 1.1. Real background delivery, isolated database, synthetic providers.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public class AutomaticFlowTests : DatabaseTest
{
    protected override bool BackgroundWorkers => true;

    private static async Task<JsonElement> Read(HttpResponseMessage response, params HttpStatusCode[] allowed)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(allowed.Contains(response.StatusCode), $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
    private static async Task<JsonElement> Post(HttpClient client, string path, object body, params HttpStatusCode[] allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Read(await client.SendAsync(request), allowed);
    }
    private static async Task<JsonElement> WaitFor(HttpClient client, string path, Func<JsonElement, bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            var state = await Read(await client.GetAsync(path), HttpStatusCode.OK);
            if (condition(state)) return state;
            await Task.Delay(50);
        }
        throw new TimeoutException("Background delivery did not complete within its acceptance budget.");
    }

    [IntegrationFact]
    public async Task PA04_PA05_PA06_Real_workers_complete_the_flow_while_archive_and_email_fail()
    {
        App.Archive.Fail = true; App.Notifications.Fail = true;
        var hold = await Post(Citizen, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, Starts), HttpStatusCode.Created);
        var payment = await WaitFor(Citizen, $"/api/pagos/intentos/{hold.GetProperty("intentoId").GetGuid()}", state => state.GetProperty("estado").GetString() == "APROBADO");
        var appointment = payment.GetProperty("citaId").GetGuid();
        await Read(await Administrator.PutAsJsonAsync($"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }), HttpStatusCode.OK);
        var started = await Post(Inspector, "/api/inspecciones", new { citaId = appointment }, HttpStatusCode.Created); var inspection = started.GetProperty("id").GetGuid();
        await Read(await Inspector.PutAsJsonAsync($"/api/inspecciones/{inspection}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }), HttpStatusCode.OK);
        await Post(Inspector, $"/api/inspecciones/{inspection}/cierre", new { versionEsperada = 1 }, HttpStatusCode.OK);
        var timer = Stopwatch.StartNew();
        var certificate = await Post(Citizen, $"/api/inspecciones/{inspection}/certificado", new { }, HttpStatusCode.OK, HttpStatusCode.Accepted);
        var id = certificate.GetProperty("id").GetGuid();
        await WaitFor(Citizen, $"/api/certificados/{id}", state => state.GetProperty("estado").GetString() == "EMITIDO");
        var response = await Citizen.GetAsync($"/api/certificados/{id}/pdf"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }
}
