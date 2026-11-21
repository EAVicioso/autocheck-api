using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos.IniciarPago;
using AutoCheck.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoCheck.Tests.Integration;

public partial class BusinessFlowTests
{
    [IntegrationFact]
    public async Task PA12CenterConfigurationControlsReservableCapacity()
    {
        var center = await Json(await Administrator.PostAsJsonAsync("/api/centros",
            new { nombre = "Centro nuevo", direccion = "Direccion de prueba", activo = true }), HttpStatusCode.Created);
        var id = center.GetProperty("id").GetGuid();
        var path = $"/api/centros/{id}";
        var centers = await Json(await Citizen.GetAsync("/api/centros"));
        Assert.Contains(centers.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

        var hours = new { horarios = new[] { new { dia = 4, aperturaMinutos = 600, cierreMinutos = 660 } } };
        await Json(await Administrator.PutAsJsonAsync(path + "/horarios", hours));
        var ramp = await Json(await Administrator.PostAsJsonAsync(path + "/rampas",
            new { nombre = "Rampa nueva", activa = true }), HttpStatusCode.Created);
        var rampId = ramp.GetProperty("id").GetGuid();
        var detail = await Json(await Citizen.GetAsync(path));
        Assert.Equal(rampId, Assert.Single(detail.GetProperty("rampas").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(600, Assert.Single(detail.GetProperty("horarios").EnumerateArray()).GetProperty("aperturaMinutos").GetInt32());

        var availability = await Json(await Citizen.GetAsync(path + "/disponibilidad?fecha=2026-11-12"));
        Assert.Equal("America/Santo_Domingo", availability.GetProperty("zonaHoraria").GetString());
        var blocks = availability.GetProperty("bloques").EnumerateArray().ToArray();
        Assert.Equal(2, blocks.Length);
        Assert.Equal(Starts, blocks[0].GetProperty("inicioUtc").GetDateTimeOffset());
        Assert.Equal(Starts.AddMinutes(30), blocks[1].GetProperty("inicioUtc").GetDateTimeOffset());
        Assert.All(blocks, block => Assert.Equal("LIBRE", block.GetProperty("estado").GetString()));

        await Json(await Administrator.PatchAsJsonAsync(path + $"/rampas/{rampId}", new { nombre = "Rampa nueva", activa = false }));
        var reduced = await Json(await Citizen.GetAsync(path + "/disponibilidad?fecha=2026-11-12"));
        Assert.Empty(reduced.GetProperty("bloques").EnumerateArray());

        await Json(await Administrator.PatchAsJsonAsync(path, new { nombre = "Centro nuevo", direccion = "Direccion de prueba", activo = false }));
        using var inactive = await Citizen.GetAsync(path + "/disponibilidad?fecha=2026-11-12");
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);

        var events = await WithDb(db => db.Outbox.Where(x => x.Kind == "DISPONIBILIDAD").Select(x => x.Payload).ToArrayAsync());
        Assert.Equal(4, events.Length);
        Assert.All(events, payload =>
        {
            using var json = JsonDocument.Parse(payload);
            Assert.Equal(id, json.RootElement.GetProperty("centroId").GetGuid());
        });
    }

    [IntegrationFact]
    public async Task PA12OperationsRespectCenterScopeAndPreserveCommitments()
    {
        var path = $"/api/centros/{CenterId}";
        var noHours = new { horarios = Array.Empty<object>() };
        using var forbidden = await Citizen.PutAsJsonAsync(path + "/horarios", noHours);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var duplicateDays = new { horarios = new[] { new { dia = 4, aperturaMinutos = 600, cierreMinutos = 660 }, new { dia = 4, aperturaMinutos = 660, cierreMinutos = 720 } } };
        using var invalid = await Administrator.PutAsJsonAsync(path + "/horarios", duplicateDays);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await Json(await Start(), HttpStatusCode.Created);
        using var scheduleConflict = await Administrator.PutAsJsonAsync(path + "/horarios", noHours);
        Assert.Equal(HttpStatusCode.Conflict, scheduleConflict.StatusCode);
        using var centerConflict = await Administrator.PatchAsJsonAsync(path, new { nombre = "Centro", direccion = "Prueba", activo = false });
        Assert.Equal(HttpStatusCode.Conflict, centerConflict.StatusCode);
        Assert.Equal(7, await WithDb(db => db.Schedules.CountAsync()));
        Assert.Equal(1, await WithDb(db => db.Outbox.CountAsync(x => x.Kind == "DISPONIBILIDAD")));

        await WithDb(async db =>
        {
            var admin = await db.Users.SingleAsync(x => x.Id == AdminId);
            admin.CenterId = CenterId;
            return await db.SaveChangesAsync();
        });

        using var otherCenter = await Administrator.PutAsJsonAsync($"/api/centros/{Guid.NewGuid()}/horarios", noHours);
        Assert.Equal(HttpStatusCode.Forbidden, otherCenter.StatusCode);
    }

    [IntegrationFact]
    public async Task PA03AbandonmentRestoresAvailabilityAndCanBeRetried()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created);
        var attempt = hold.GetProperty("intentoId").GetGuid();
        var path = $"/api/citas/retenciones/{attempt}";
        var availability = $"/api/centros/{CenterId}/disponibilidad?fecha=2026-11-12";

        var held = FindSlot(await Json(await Citizen.GetAsync(availability)));
        Assert.Equal("RETENIDO", held.GetProperty("estado").GetString());
        Assert.Equal(App.Clock.Now.AddSeconds(300), held.GetProperty("expiraUtc").GetDateTimeOffset());
        using var forbidden = await Inspector.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var first = await Citizen.DeleteAsync(path);
        using var replay = await Citizen.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        var released = FindSlot(await Json(await Citizen.GetAsync(availability)));
        Assert.Equal("LIBRE", released.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, released.GetProperty("expiraUtc").ValueKind);
        Assert.Equal(2, await WithDb(db => db.Outbox.CountAsync(x => x.Kind == "DISPONIBILIDAD")));

        var paidLate = await Webhook(attempt);
        Assert.Equal("REVERTIDO", paidLate.GetProperty("estado").GetString());
        var status = await Json(await Citizen.GetAsync($"/api/pagos/intentos/{attempt}"));
        Assert.Equal(paidLate.GetProperty("reversionId").GetGuid(), status.GetProperty("reversionId").GetGuid());
        using var hidden = await Inspector.GetAsync($"/api/pagos/intentos/{attempt}");
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        Assert.Equal(0, await WithDb(db => db.Appointments.CountAsync()));
    }

    [IntegrationFact]
    public async Task PA04ApprovalAtSlotStartReversesEvenWhileHoldIsUnexpired()
    {
        var slot = App.Clock.Now.AddMinutes(30);
        App.Clock.Advance(TimeSpan.FromMinutes(29));
        var hold = await Json(await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, CenterId, RampId, slot, DemoraSegundos: 600), "same-minute"), HttpStatusCode.Created);
        App.Clock.Advance(TimeSpan.FromSeconds(60));

        Assert.True(hold.GetProperty("expiraUtc").GetDateTimeOffset() > App.Clock.Now);
        var result = await Webhook(hold.GetProperty("intentoId").GetGuid());
        Assert.Equal("REVERTIDO", result.GetProperty("estado").GetString());
        Assert.Equal(0, await WithDb(db => db.Appointments.CountAsync()));
        Assert.Equal(0, await WithDb(db => db.Blocks.CountAsync()));
    }

    [IntegrationFact]
    public async Task PA04ConcurrentEventIdReuseAcrossCentersConfirmsOnlyOnePayment()
    {
        var otherCenter = Guid.NewGuid();
        var otherRamp = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Centers.Add(new() { Id = otherCenter, Name = "Centro secundario", Address = "Prueba" });
            db.Ramps.Add(new() { Id = otherRamp, CenterId = otherCenter, Name = "Otra rampa" });
            db.Schedules.Add(new() { CenterId = otherCenter, Day = 4, Opens = 480, Closes = 1080 });
            return await db.SaveChangesAsync();
        });

        var first = await Json(await Start(), HttpStatusCode.Created);
        var second = await Json(await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, otherCenter, otherRamp, Starts.AddMinutes(30), DemoraSegundos: 600), "other-center"), HttpStatusCode.Created);
        const string eventId = "shared-provider-event";
        var firstApproval = SendWebhook(first.GetProperty("intentoId").GetGuid(), "APROBADO", eventId);
        var secondApproval = SendWebhook(second.GetProperty("intentoId").GetGuid(), "APROBADO", eventId);
        await Task.WhenAll(firstApproval, secondApproval);
        using var firstResponse = await firstApproval;
        using var secondResponse = await secondApproval;

        var responses = new[] { firstResponse, secondResponse };
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("EVENTO_CONFLICTO", (await Json(conflict, HttpStatusCode.Conflict)).GetProperty("codigo").GetString());
        Assert.Equal(1, await WithDb(db => db.PaymentEvents.CountAsync()));
        Assert.Equal(1, await WithDb(db => db.Appointments.CountAsync()));
    }

    [IntegrationFact]
    public async Task PA04CancellationSupersedesConfirmationAndNotificationRetriesPreserveIdentity()
    {
        var hold = await Json(await Start(), HttpStatusCode.Created);
        var attempt = hold.GetProperty("intentoId").GetGuid();
        var payment = await Webhook(attempt);
        var appointment = payment.GetProperty("citaId").GetGuid();
        var path = $"/api/citas/{appointment}";
        var detail = await Json(await Citizen.GetAsync(path));
        Assert.Equal("CONFIRMADA", detail.GetProperty("estado").GetString());
        using var unassigned = await Inspector.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);

        await Json(await Send(Citizen, HttpMethod.Post, path + "/cancelacion", new { motivo = "Reagendar" }, "cancel"));
        App.Notifications.Fail = true;
        await DispatchNotification("CITA_CONFIRMADA");
        await DispatchNotification("CITA_CANCELADA");
        var pending = await WithDb(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Kind == "CITA_CANCELADA"));
        Assert.Equal(appointment, pending.AggregateId);
        Assert.Equal(1, pending.Attempts);
        Assert.Null(pending.Completed);
        Assert.True(await WithDb(db => db.Audit.AnyAsync(x => x.Operation == "CITA_CONFIRMADA" && x.Outcome == "SUPERADO_POR_CANCELACION")));

        App.Notifications.Fail = false;
        App.Clock.Advance(TimeSpan.FromSeconds(2));
        await DispatchNotification("CITA_CANCELADA");
        var delivered = await WithDb(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Kind == "CITA_CANCELADA"));
        Assert.Equal(pending.Id, delivered.Id);
        Assert.NotNull(delivered.Completed);
        Assert.Equal("CANCELADA", (await Json(await Citizen.GetAsync(path))).GetProperty("estado").GetString());
        await Json(await Start(), HttpStatusCode.Created);
    }

    private JsonElement FindSlot(JsonElement availability) => Assert.Single(availability.GetProperty("bloques").EnumerateArray(),
        block => block.GetProperty("rampaId").GetGuid() == RampId && block.GetProperty("inicioUtc").GetDateTimeOffset() == Starts);

    private async Task DispatchNotification(string kind)
    {
        using var scope = App.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        Assert.True(await dispatcher.DispatchOne(default, kind));
    }
}
