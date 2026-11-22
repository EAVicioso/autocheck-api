using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos.IniciarPago;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
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
        await AssertAvailabilityEvents(id, 1);
        await Json(await Administrator.PutAsJsonAsync(path + "/horarios", hours));
        await AssertAvailabilityEvents(id, 1);
        var ramp = await Json(await Administrator.PostAsJsonAsync(path + "/rampas",
            new { nombre = "Rampa nueva", activa = true }), HttpStatusCode.Created);
        await AssertAvailabilityEvents(id, 2);
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
        await AssertAvailabilityEvents(id, 3);
        var reduced = await Json(await Citizen.GetAsync(path + "/disponibilidad?fecha=2026-11-12"));
        Assert.Empty(reduced.GetProperty("bloques").EnumerateArray());

        await Json(await Administrator.PatchAsJsonAsync(path, new { nombre = "Centro nuevo", direccion = "Direccion de prueba", activo = false }));
        using var inactive = await Citizen.GetAsync(path + "/disponibilidad?fecha=2026-11-12");
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        await AssertAvailabilityEvents(id, 4);
        var publishedTo = new List<Guid>();
        App.Availability.OnPublish = publishedTo.Add;
        for (var i = 0; i < 4; i++)
        {
            await DispatchNotification("DISPONIBILIDAD");
        }

        Assert.Equal(new[] { id, id, id, id }, publishedTo);
    }

    private async Task AssertAvailabilityEvents(Guid id, int count)
    {
        var events = await WithDb(db => db.Outbox.Where(x => x.Kind == "DISPONIBILIDAD").Select(x => x.Payload).ToArrayAsync());
        Assert.Equal(count, events.Length);
        Assert.All(events, payload =>
        {
            using var json = JsonDocument.Parse(payload);
            Assert.Equal(id, json.RootElement.GetProperty("centroId").GetGuid());
        });
    }

    [IntegrationFact]
    public async Task PA12aTwoRampsCanConfirmTheSameTimeSlot()
    {
        var ramp = await Json(await Administrator.PostAsJsonAsync($"/api/centros/{CenterId}/rampas",
            new { nombre = "Rampa paralela", activa = true }), HttpStatusCode.Created);
        var otherRamp = ramp.GetProperty("id").GetGuid();
        using var otherCitizen = App.CreateClient();
        var otherVehicle = await AddSectionBCitizen(otherCitizen);
        var first = await Json(await Start(), HttpStatusCode.Created);
        var second = await Json(await Send(otherCitizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(otherVehicle, CenterId, otherRamp, Starts, DemoraSegundos: 600), "parallel-ramp"), HttpStatusCode.Created);
        Assert.Equal("APROBADO", (await Webhook(first.GetProperty("intentoId").GetGuid())).GetProperty("estado").GetString());
        Assert.Equal("APROBADO", (await Webhook(second.GetProperty("intentoId").GetGuid())).GetProperty("estado").GetString());
        using var third = await Start();
        Assert.Equal("BLOQUE_NO_DISPONIBLE", (await Json(third, HttpStatusCode.Conflict)).GetProperty("codigo").GetString());

        var appointments = await WithDb(db => db.Appointments.AsNoTracking().ToListAsync());
        Assert.Equal(2, appointments.Count);
        Assert.All(appointments, appointment =>
        {
            Assert.Equal("CONFIRMADA", appointment.State);
            Assert.Equal(CenterId, appointment.CenterId);
            Assert.Equal(Starts, appointment.Starts);
        });
        Assert.Contains(appointments, appointment => appointment.RampId == RampId);
        Assert.Contains(appointments, appointment => appointment.RampId == otherRamp);
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

        var (otherCenterId, otherRampId) = await AddSectionBCenter();
        await WithDb(async db =>
        {
            var admin = await db.Users.SingleAsync(x => x.Id == AdminId);
            admin.CenterId = CenterId;
            return await db.SaveChangesAsync();
        });

        using var otherCenter = await Administrator.PutAsJsonAsync($"/api/centros/{otherCenterId}/horarios", noHours);
        Assert.Equal(HttpStatusCode.Forbidden, otherCenter.StatusCode);
        var rampUpdate = new { nombre = "Cambio fuera de alcance", activa = false };
        using var otherRamp = await Administrator.PatchAsJsonAsync($"/api/centros/{otherCenterId}/rampas/{otherRampId}", rampUpdate);
        Assert.Equal(HttpStatusCode.Forbidden, otherRamp.StatusCode);
        using var mismatchedRamp = await Administrator.PatchAsJsonAsync(path + $"/rampas/{otherRampId}", rampUpdate);
        Assert.Equal(HttpStatusCode.NotFound, mismatchedRamp.StatusCode);
        using var mismatchedHold = await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, CenterId, otherRampId, Starts, DemoraSegundos: 600), "mismatched-ramp");
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedHold.StatusCode);
        Assert.True(await WithDb(db => db.Ramps.Where(x => x.Id == otherRampId).Select(x => x.Active).SingleAsync()));
        Assert.Equal(1, await WithDb(db => db.Schedules.CountAsync(x => x.CenterId == otherCenterId)));
        await AssertAvailabilityEvents(CenterId, 1);
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
        var releases = await Task.WhenAll(Citizen.DeleteAsync(path), Citizen.DeleteAsync(path));
        using var first = releases[0];
        using var replay = releases[1];
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        var released = FindSlot(await Json(await Citizen.GetAsync(availability)));
        Assert.Equal("LIBRE", released.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, released.GetProperty("expiraUtc").ValueKind);
        await AssertAvailabilityEvents(CenterId, 2);

        var replacement = await Json(await Start(), HttpStatusCode.Created);
        using var staleRelease = await Citizen.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, staleRelease.StatusCode);
        var paidLate = await Webhook(attempt);
        Assert.Equal("REVERTIDO", paidLate.GetProperty("estado").GetString());
        var status = await Json(await Citizen.GetAsync($"/api/pagos/intentos/{attempt}"));
        Assert.Equal(paidLate.GetProperty("reversionId").GetGuid(), status.GetProperty("reversionId").GetGuid());
        using var hidden = await Inspector.GetAsync($"/api/pagos/intentos/{attempt}");
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        Assert.Equal(0, await WithDb(db => db.Appointments.CountAsync()));
        Assert.Equal(replacement.GetProperty("intentoId").GetGuid(), await WithDb(db => db.Blocks.Select(x => x.AttemptId).SingleAsync()));
        Assert.Equal("RETENIDO", FindSlot(await Json(await Citizen.GetAsync(availability))).GetProperty("estado").GetString());
        await AssertAvailabilityEvents(CenterId, 3);
    }

    [IntegrationFact]
    public async Task PA03bApprovalAt299SecondsConfirmsOnceEvenAfterSlotStarts()
    {
        var slot = App.Clock.Now.AddMinutes(30);
        App.Clock.Advance(TimeSpan.FromMinutes(29));
        var hold = await Json(await Send(Citizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(VehicleId, CenterId, RampId, slot, DemoraSegundos: 600), "same-minute"), HttpStatusCode.Created);
        App.Clock.Advance(TimeSpan.FromSeconds(299));

        Assert.True(slot < App.Clock.Now);
        Assert.Equal(App.Clock.Now.AddSeconds(1), hold.GetProperty("expiraUtc").GetDateTimeOffset());
        var attempt = hold.GetProperty("intentoId").GetGuid();
        var result = await Webhook(attempt, eventId: "approval-at-299");
        Assert.Equal("APROBADO", result.GetProperty("estado").GetString());
        App.Clock.Advance(TimeSpan.FromSeconds(2));
        var replay = await Webhook(attempt, eventId: "approval-at-299");
        Assert.Equal(result.GetProperty("citaId").GetGuid(), replay.GetProperty("citaId").GetGuid());
        Assert.Equal(slot, await WithDb(db => db.Appointments.Select(x => x.Starts).SingleAsync()));
        Assert.Equal("CONFIRMADO", await WithDb(db => db.Blocks.Select(x => x.State).SingleAsync()));
    }

    [IntegrationFact]
    public async Task PA04ConcurrentEventIdReuseAcrossCentersConfirmsOnlyOnePayment()
    {
        var (otherCenter, otherRamp) = await AddSectionBCenter();
        using var otherCitizen = App.CreateClient();
        var otherVehicle = await AddSectionBCitizen(otherCitizen);
        var first = await Json(await Start(), HttpStatusCode.Created);
        var second = await Json(await Send(otherCitizen, HttpMethod.Post, "/api/pagos/intentos",
            new StartPaymentRequest(otherVehicle, otherCenter, otherRamp, Starts, DemoraSegundos: 600), "other-center"), HttpStatusCode.Created);
        var firstAttempt = first.GetProperty("intentoId").GetGuid();
        var secondAttempt = second.GetProperty("intentoId").GetGuid();
        const string eventId = "shared-provider-event";

        using var scope = App.Services.CreateScope();
        var locker = scope.ServiceProvider.GetRequiredService<AutoCheckDb>();
        await using var transaction = await locker.Database.BeginTransactionAsync();
        await locker.Database.ExecuteSqlRawAsync("""LOCK TABLE autocheck."PaymentEvent" IN SHARE MODE""");
        var firstApproval = SendWebhook(firstAttempt, "APROBADO", eventId);
        var secondApproval = SendWebhook(secondAttempt, "APROBADO", eventId);
        bool bothWaiting;
        try
        {
            bothWaiting = await WaitForPaymentEventWriters();
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        await Task.WhenAll(firstApproval, secondApproval);
        using var firstResponse = await firstApproval;
        using var secondResponse = await secondApproval;
        Assert.True(bothWaiting, "Both webhooks must reach the PostgreSQL write before either can confirm.");
        var responses = new[] { firstResponse, secondResponse };
        var approval = await Json(Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal("APROBADO", approval.GetProperty("estado").GetString());
        var winner = approval.GetProperty("intentoId").GetGuid();
        var loser = winner == firstAttempt ? secondAttempt : firstAttempt;
        var appointment = approval.GetProperty("citaId").GetGuid();
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("EVENTO_CONFLICTO", (await Json(conflict, HttpStatusCode.Conflict)).GetProperty("codigo").GetString());
        Assert.Equal(appointment, (await Webhook(winner, eventId: eventId)).GetProperty("citaId").GetGuid());
        using var rejectedReplay = await SendWebhook(loser, "APROBADO", eventId);
        Assert.Equal("EVENTO_CONFLICTO", (await Json(rejectedReplay, HttpStatusCode.Conflict)).GetProperty("codigo").GetString());
        Assert.Equal(winner, await WithDb(db => db.PaymentEvents.Select(x => x.AttemptId).SingleAsync()));
        Assert.Equal(appointment, await WithDb(db => db.Appointments.Select(x => x.Id).SingleAsync()));
        Assert.Equal("PENDIENTE", await WithDb(db => db.Payments.Where(x => x.Id == loser).Select(x => x.State).SingleAsync()));
        Assert.Equal("RETENIDO", await WithDb(db => db.Blocks.Where(x => x.AttemptId == loser).Select(x => x.State).SingleAsync()));
        Assert.Equal(1, await WithDb(db => db.Outbox.CountAsync(x => x.Kind == "CITA_CONFIRMADA")));
    }

    private async Task<bool> WaitForPaymentEventWriters()
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            var waiters = await WithDb(db => db.Database.SqlQueryRaw<int>("""
                SELECT count(*)::int AS "Value" FROM pg_locks
                WHERE relation = 'autocheck."PaymentEvent"'::regclass
                  AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                  AND mode = 'RowExclusiveLock' AND NOT granted
                """).SingleAsync());
            if (waiters == 2)
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private async Task<(Guid Center, Guid Ramp)> AddSectionBCenter()
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

        return (otherCenter, otherRamp);
    }

    private async Task<Guid> AddSectionBCitizen(HttpClient client)
    {
        var authId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        await WithDb(async db =>
        {
            var user = new UserAccount { AuthId = authId, Email = "otro-ciudadano@example.test", Role = Roles.Citizen };
            var sample = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == VehicleId);
            db.Users.Add(user);
            db.Sessions.Add(new() { Id = sessionId, UserId = user.Id, LastActivity = App.Clock.Now });
            db.Vehicles.Add(new()
            {
                Id = vehicleId, OwnerId = user.Id, Plate = "A654321", Vin = "987654321ABCDEFGH",
                ModelId = sample.ModelId, ColorId = sample.ColorId, Year = sample.Year
            });
            return await db.SaveChangesAsync();
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FakeIdentity.Token(authId, sessionId));
        return vehicleId;
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
        var sends = new List<(Guid Appointment, string Kind, Guid Delivery)>();
        App.Notifications.OnSend = (item, kind, delivery) => sends.Add((item.Id, kind, delivery));
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
        Assert.Collection(sends,
            send => Assert.Equal((appointment, "CITA_CANCELADA", pending.Id), send),
            send => Assert.Equal((appointment, "CITA_CANCELADA", pending.Id), send));
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
