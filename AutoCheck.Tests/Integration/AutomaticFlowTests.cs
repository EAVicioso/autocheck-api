// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1. Real background delivery, isolated database, synthetic providers.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos;
using AutoCheck.Api.Features.Pagos.IniciarPago;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public class AutomaticFlowTests : DatabaseTest
{
    private static readonly DateTimeOffset MidnightInSantoDomingo = new(2026, 11, 12, 4, 0, 0, TimeSpan.Zero);
    protected override bool BackgroundWorkers => true;
    protected override DateTimeOffset? InitialUtc => MidnightInSantoDomingo.AddSeconds(-1);

    private static async Task<JsonElement> Read(HttpResponseMessage response, params HttpStatusCode[] allowed)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(allowed.Contains(response.StatusCode), $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement> Post(HttpClient client, string path, object body, params HttpStatusCode[] allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Read(await client.SendAsync(request), allowed);
    }

    private static async Task<JsonElement> WaitFor(HttpClient client, string path, Func<JsonElement, bool> condition, Action? beforeRetry = null)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            var state = await Read(await client.GetAsync(path), HttpStatusCode.OK);
            if (condition(state))
            {
                return state;
            }

            beforeRetry?.Invoke();
            await Task.Delay(50);
        }

        throw new TimeoutException("Background delivery did not complete within its acceptance budget.");
    }

    [IntegrationFact]
    public async Task PA04PA05PA06RealWorkersCompleteTheFlowWhileArchiveAndEmailFail()
    {
        App.Archive.Fail = true;
        App.Notifications.Fail = true;
        var inspection = await StartRegisteredInspection(Starts);
        await Post(Inspector, $"/api/inspecciones/{inspection}/cierre", new { versionEsperada = 1 }, HttpStatusCode.OK);
        var timer = Stopwatch.StartNew();
        var certificate = await Post(Citizen, $"/api/inspecciones/{inspection}/certificado", new { }, HttpStatusCode.OK, HttpStatusCode.Accepted);
        var id = certificate.GetProperty("id").GetGuid();
        await WaitFor(Citizen, $"/api/certificados/{id}", state => state.GetProperty("estado").GetString() == "EMITIDO");
        var response = await Citizen.GetAsync($"/api/certificados/{id}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }

    [IntegrationFact]
    public async Task PA08DailyConsolidationIsVisibleBeforeFiveMinutesPastSantoDomingoMidnight()
    {
        var lastOfPreviousDay = await StartRegisteredInspection(Starts);
        var firstOfNextDay = await StartRegisteredInspection(Starts.AddMinutes(30));
        await Post(Inspector, $"/api/inspecciones/{lastOfPreviousDay}/cierre", new { versionEsperada = 1 }, HttpStatusCode.OK);
        App.Clock.Now = MidnightInSantoDomingo;
        await Post(Inspector, $"/api/inspecciones/{firstOfNextDay}/cierre", new { versionEsperada = 1 }, HttpStatusCode.OK);

        var path = $"/api/reportes/inspecciones?desde=2026-11-11&hasta=2026-11-12&centroId={CenterId}&rampaId={RampId}&inspectorId={InspectorId}";
        var report = await WaitFor(Administrator, path, state => state.GetProperty("cerradas").GetInt32() == 2,
            () => App.Clock.Advance(TimeSpan.FromSeconds(1)));

        var cutoff = MidnightInSantoDomingo.AddMinutes(5);
        var checkpoint = report.GetProperty("ultimaActualizacionUtc").GetDateTimeOffset();
        Assert.True(checkpoint >= MidnightInSantoDomingo && checkpoint < cutoff);
        Assert.True(App.Clock.Now < cutoff);
        Assert.Collection(report.GetProperty("items").EnumerateArray(),
            row => AssertDailyClose(row, "2026-11-11"),
            row => AssertDailyClose(row, "2026-11-12"));
    }

    private static void AssertDailyClose(JsonElement row, string day)
    {
        Assert.Equal(day, row.GetProperty("dia").GetString());
        Assert.Equal(1, row.GetProperty("cerradas").GetInt32());
    }

    private async Task<Guid> StartRegisteredInspection(DateTimeOffset starts)
    {
        var hold = await Post(Citizen, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, starts), HttpStatusCode.Created);
        var payment = await WaitFor(Citizen, $"/api/pagos/intentos/{hold.GetProperty("intentoId").GetGuid()}", state => state.GetProperty("estado").GetString() == "APROBADO");
        var appointment = payment.GetProperty("citaId").GetGuid();
        await Read(await Administrator.PutAsJsonAsync($"/api/citas/{appointment}/inspector", new { inspectorId = InspectorId }), HttpStatusCode.OK);
        var started = await Post(Inspector, "/api/inspecciones", new { citaId = appointment }, HttpStatusCode.Created);
        var inspection = started.GetProperty("id").GetGuid();
        await Read(await Inspector.PutAsJsonAsync($"/api/inspecciones/{inspection}/registro", new { versionEsperada = 0, defectos = Array.Empty<object>() }), HttpStatusCode.OK);
        return inspection;
    }
}
