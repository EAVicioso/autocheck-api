using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using AutoCheck.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class LifecycleRemediationTests : DatabaseTest
{
    private static readonly string[] AggregateDeliveryKinds = ["COPIA_MONGO", "PAGO_SANDBOX", "CITA_CONFIRMADA"];
    [IntegrationFact]
    public async Task LegacySqlAdvancesVersionsWithoutDoubleIncrementingExplicitWrites()
    {
        await WithDb(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE autocheck.\"UserAccount\" SET \"Name\" = {"Legacy"} WHERE \"Id\" = {CitizenId}");
            return await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE autocheck.\"Vehicle\" SET \"Year\" = {2021} WHERE \"Id\" = {VehicleId}");
        });
        using var staleProfile = await Citizen.PatchAsJsonAsync("/api/usuarios/me", new { nombre = "Perfil", celular = "", version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, staleProfile.StatusCode);
        using var staleVehicle = await Citizen.PatchAsJsonAsync($"/api/vehiculos/{VehicleId}", new
        {
            matricula = "A123456", vin = "123456789ABCDEFGH", anio = 2022, version = 1,
            modeloId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            colorId = Guid.Parse("20000000-0000-0000-0000-000000000003")
        });
        Assert.Equal(HttpStatusCode.Conflict, staleVehicle.StatusCode);
        using var updated = await Citizen.PatchAsJsonAsync("/api/usuarios/me", new { nombre = "Perfil", celular = "", version = 2 });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(3L, await WithDb(db => db.Users.Where(x => x.Id == CitizenId).Select(x => x.Version).SingleAsync()));
        Assert.Equal(2L, await WithDb(db => db.Vehicles.Where(x => x.Id == VehicleId).Select(x => x.Version).SingleAsync()));
    }

    [IntegrationFact]
    public Task QueuedAccessChangeRechecksRevokedPermission() => AssertQueuedAdminMutationDenied(archive: false);

    [IntegrationFact]
    public Task QueuedUserArchiveRechecksNarrowedCenter() => AssertQueuedAdminMutationDenied(archive: true);

    private async Task AssertQueuedAdminMutationDenied(bool archive)
    {
        var route = $"/api/usuarios/{CitizenId}";
        using var response = await WithDb(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.LockAsync("admin-continuity", default);
            var pending = archive
                ? Administrator.DeleteAsync(route + "?version=1")
                : Administrator.PatchAsJsonAsync(route + "/acceso", new { rol = Roles.Citizen, permisos = Array.Empty<string>(), activo = false, version = 1 });
            await WaitForAdvisoryWaiters(1);
            if (archive)
            {
                await db.Users.Where(x => x.Id == AdminId).ExecuteUpdateAsync(u => u.SetProperty(x => x.CenterId, CenterId));
            }
            else
            {
                await db.Users.Where(x => x.Id == AdminId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Permissions, Array.Empty<string>()));
            }
            await tx.CommitAsync();
            return await pending;
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await WithDb(db => db.Users.AnyAsync(x => x.Id == CitizenId && x.Active && x.ArchivedAt == null && x.Version == 1)));
    }

    [IntegrationFact]
    public async Task VehicleOwnerWithoutCitizenRoleCannotArchive()
    {
        await WithDb(db => db.Users.Where(x => x.Id == CitizenId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, Roles.Inspector).SetProperty(x => x.CenterId, CenterId)));
        using var response = await Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await WithDb(db => db.Vehicles.AnyAsync(x => x.Id == VehicleId && x.ArchivedAt == null)));
    }

    [IntegrationFact]
    public Task WebhookConfirmationSerializesWithUserArchive() => AssertPaymentSerialization(webhook: true);

    [IntegrationFact]
    public Task OutboxConfirmationSerializesWithVehicleArchive() => AssertPaymentSerialization(webhook: false);

    [IntegrationFact]
    public Task UserArchiveAtExpirationWinsAgainstWebhook() => AssertPaymentSerialization(webhook: true, archiveFirst: true);

    [IntegrationFact]
    public Task VehicleArchiveAtExpirationWinsAgainstOutbox() => AssertPaymentSerialization(webhook: false, archiveFirst: true);

    private async Task AssertPaymentSerialization(bool webhook, bool archiveFirst = false)
    {
        Citizen.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var hold = await Citizen.PostAsJsonAsync("/api/pagos/intentos", new { vehiculoId = VehicleId, centroId = CenterId, rampaId = RampId, inicioUtc = Starts });
        Assert.Equal(HttpStatusCode.Created, hold.StatusCode);
        var attempt = await WithDb(db => db.Payments.Select(x => x.Id).SingleAsync());
        using var archive = await WithDb(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.LockAsync("user:" + CitizenId, default);
            Task<bool> Confirm() => webhook ? ConfirmWebhook(attempt) : Dispatch("PAGO_SANDBOX", attempt);

            var pending = archiveFirst ? ArchivePaymentResource(webhook) : null;
            var confirmation = archiveFirst ? null : Confirm();
            await WaitForAdvisoryWaiters(1);
            pending ??= ArchivePaymentResource(webhook);
            confirmation ??= Confirm();
            await WaitForAdvisoryWaiters(2);
            if (archiveFirst)
            {
                App.Clock.Advance(TimeSpan.FromSeconds(300));
            }
            await tx.CommitAsync();
            Assert.True(await confirmation);
            return await pending;
        });
        if (archiveFirst)
        {
            await AssertArchiveWins(archive, attempt, webhook);
            return;
        }

        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);
        Assert.True(await WithDb(db => db.Appointments.AnyAsync(x => x.AttemptId == attempt && x.State == "CONFIRMADA")));
        Assert.True(await WithDb(db => db.Users.AnyAsync(x => x.Id == CitizenId && x.ArchivedAt == null)));
        Assert.True(await WithDb(db => db.Vehicles.AnyAsync(x => x.Id == VehicleId && x.ArchivedAt == null)));
    }

    private Task<HttpResponseMessage> ArchivePaymentResource(bool archiveUser) => archiveUser
        ? Administrator.DeleteAsync($"/api/usuarios/{CitizenId}?version=1")
        : Citizen.DeleteAsync($"/api/vehiculos/{VehicleId}?version=1");

    private async Task AssertArchiveWins(HttpResponseMessage archive, Guid attempt, bool webhook)
    {
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.False(await WithDb(db => db.Appointments.AnyAsync(x => x.AttemptId == attempt)));
        var reverse = await WithDb(db => db.Payments.Where(x => x.Id == attempt && x.State == "REVERTIDO").Select(x => x.ReversalId).SingleAsync());
        Assert.NotNull(reverse);
        if (webhook)
        {
            Assert.True(await ConfirmWebhook(attempt));
        }
        else
        {
            Assert.False(await Dispatch("PAGO_SANDBOX", attempt));
        }
        Assert.Equal(reverse, await WithDb(db => db.Payments.Where(x => x.Id == attempt).Select(x => x.ReversalId).SingleAsync()));
        Assert.False(await WithDb(db => db.Appointments.AnyAsync(x => x.AttemptId == attempt)));
    }

    [IntegrationFact]
    public async Task Auth408AndTimeoutReturn503WithoutChangingLoginCounter()
    {
        const string email = "ciudadano@example.test";
        await WithDb(async db =>
        {
            db.AccountLocks.Add(new() { Email = email, Failures = 2 });
            return await db.SaveChangesAsync();
        });
        foreach (var throwsTimeout in new[] { false, true })
        {
            using var http = new HttpClient(new UnavailableAuth(throwsTimeout));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://example.test",
                ["Supabase:PublishableKey"] = Guid.NewGuid().ToString()
            }).Build();
            using var app = App.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityProvider>();
                services.AddSingleton<IIdentityProvider>(new SupabaseIdentity(http, config));
            }));
            using var client = app.CreateClient();
            foreach (var (path, body) in new (string, object)[]
            {
                ("login", new { correo = email, contrasena = "synthetic" }),
                ("refresh", new { refreshToken = "synthetic-refresh" }),
                ("registro", new { correo = "new@example.test", contrasena = "Synthetic12345", nombre = "Prueba" })
            })
            {
                using var result = await client.PostAsJsonAsync("/api/auth/" + path, body);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
                using var problem = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
                Assert.Equal("SERVICIO_NO_DISPONIBLE", problem.RootElement.GetProperty("codigo").GetString());
            }
        }
        Assert.Equal(2, await WithDb(db => db.AccountLocks.Where(x => x.Email == email).Select(x => x.Failures).SingleAsync()));
    }

    [IntegrationFact]
    public async Task MalformedOutboxPayloadIsRecordedOnceAndNeverRedispatched()
    {
        var message = new OutboxMessage { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Payload = """{"centroId":false}""", Available = App.Clock.Now };
        await WithDb(async db =>
        {
            db.Outbox.Add(message);
            return await db.SaveChangesAsync();
        });
        Assert.True(await Dispatch(message.Kind, message.AggregateId));
        App.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(await Dispatch(message.Kind, message.AggregateId));
        var failed = await WithDb(db => db.Outbox.SingleAsync(x => x.Id == message.Id));
        Assert.Equal("PAYLOAD_INVALIDO", failed.FailureCode);
        Assert.NotNull(failed.FailedAt);
        Assert.Null(failed.Completed);
        Assert.Equal(1, failed.Attempts);
    }

    [IntegrationFact]
    public async Task MissingAggregatesAreDeadLetteredWithoutMarkingDeliveryComplete()
    {
        var messages = AggregateDeliveryKinds
            .Select(kind => new OutboxMessage { Kind = kind, AggregateId = Guid.NewGuid(), Available = App.Clock.Now })
            .ToArray();
        await WithDb(async db =>
        {
            db.Outbox.AddRange(messages);
            return await db.SaveChangesAsync();
        });

        foreach (var message in messages)
        {
            Assert.True(await Dispatch(message.Kind, message.AggregateId));
        }
        App.Clock.Advance(TimeSpan.FromMinutes(10));
        foreach (var message in messages)
        {
            Assert.False(await Dispatch(message.Kind, message.AggregateId));
            var failed = await WithDb(db => db.Outbox.SingleAsync(x => x.Id == message.Id));
            Assert.Equal("AGREGADO_NO_ENCONTRADO", failed.FailureCode);
            Assert.NotNull(failed.FailedAt);
            Assert.Null(failed.Completed);
            Assert.Equal(1, failed.Attempts);
        }
    }

    [IntegrationFact]
    public async Task AuditEntityFilterCombinesLegacyAndNewAccessEvents()
    {
        await WithDb(async db =>
        {
            db.Audit.Add(new() { ActorId = AdminId, At = App.Clock.Now, Operation = "CAMBIO_ACCESO", Entity = CitizenId.ToString(), Outcome = "LEGADO", Correlation = "legacy-access" });
            return await db.SaveChangesAsync();
        });
        using var changed = await Administrator.PatchAsJsonAsync($"/api/usuarios/{CitizenId}/acceso",
            new { rol = Roles.Citizen, permisos = Array.Empty<string>(), activo = true, version = 1 });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var response = await Administrator.GetAsync($"/api/auditorias?operacion=CAMBIO_ACCESO&entidad={CitizenId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.All(items.EnumerateArray(), item => Assert.Equal(CitizenId.ToString(), item.GetProperty("entidad").GetString()));
    }

    private async Task<bool> ConfirmWebhook(Guid attempt)
    {
        var body = ApiReply.Serialize(new SandboxEvent(Guid.NewGuid().ToString(), attempt, "APROBADO"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pagos/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var key = App.Services.GetRequiredService<IConfiguration>()["Sandbox:WebhookSecret"]!;
        request.Headers.Add("X-Sandbox-Signature", Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body))));
        using var response = await Citizen.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> Dispatch(string kind, Guid aggregate)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default, kind, aggregate);
    }

    private Task<bool> WaitForAdvisoryWaiters(int count) => WithDb(async db =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        const string sql = """SELECT count(*)::integer AS "Value" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())""";
        while (await db.Database.SqlQueryRaw<int>(sql).SingleAsync(deadline.Token) < count)
        {
            await Task.Delay(20, deadline.Token);
        }
        return true;
    });

    private sealed class UnavailableAuth(bool throwsTimeout) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throwsTimeout ? Task.FromException<HttpResponseMessage>(new TimeoutException()) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout));
    }
}
