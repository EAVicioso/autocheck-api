using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoCheck.Api.Features.Pagos.IniciarPago;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Tests.Infrastructure;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoCheck.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SmtpFlowTests : DatabaseTest
{
    private WebApplicationFactory<Program> WithSmtp(LoopbackSmtpServer server, bool workers)
    {
        var settings = SmtpDeliveryTests.Settings(server.Port);
        var factory = App.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Workers:Enabled"] = workers.ToString(),
            ["Smtp:Host"] = settings.Host,
            ["Smtp:Port"] = settings.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Smtp:Username"] = settings.Username,
            ["Smtp:Password"] = settings.Password,
            ["Smtp:From"] = settings.From
        })).ConfigureTestServices(services =>
        {
            services.RemoveAll<IAppointmentNotifications>();
            services.AddScoped<IAppointmentNotifications, AppointmentNotifications>();
            services.RemoveAll<ISmtpClient>();
            services.AddScoped<ISmtpClient>(_ => server.TrustedClient());
        }));
        factory.UseKestrel(0);
        return factory;
    }

    private static async Task<JsonElement> Post(HttpClient client, string path, object body, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await condition())
        {
            await Task.Delay(25, deadline.Token);
        }
    }

    [IntegrationFact]
    public async Task PA13RealHttpAndWorkersDeliverConfirmationAndCancellationOverTlsSmtp()
    {
        await using var server = new LoopbackSmtpServer();
        await using var factory = WithSmtp(server, workers: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Citizen.DefaultRequestHeaders.Authorization;
        var hold = await Post(client, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, Starts), HttpStatusCode.Created);
        var attempt = hold.GetProperty("intentoId").GetGuid();
        await WaitFor(() => WithDb(db => db.Appointments.AnyAsync(appointment => appointment.AttemptId == attempt)));
        await WaitFor(() => Task.FromResult(server.AcceptedIds.Count == 1));
        var appointment = await WithDb(db => db.Appointments.SingleAsync(appointment => appointment.AttemptId == attempt));
        var first = Assert.Single(server.Attempts);
        Assert.Equal("Cita confirmada - AutoCheck", first.Subject);
        Assert.Equal("ciudadano@example.test", Assert.Single(first.To.Mailboxes).Address);
        Assert.Equal("autocheck@example.test", Assert.Single(first.From.Mailboxes).Address);
        Assert.Contains(appointment.Id.ToString(), first.TextBody);
        Assert.DoesNotContain(LoopbackSmtpServer.Password, first.ToString());
        var deliveryId = await WithDb(db => db.Outbox.Where(message => message.Kind == "CITA_CONFIRMADA" && message.AggregateId == appointment.Id).Select(message => message.Id).SingleAsync());
        Assert.Equal($"{deliveryId:N}@example.test", first.MessageId);
        Assert.Equal(deliveryId.ToString(), first.Headers["X-AutoCheck-Delivery-Id"]);

        await Post(client, $"/api/citas/{appointment.Id}/cancelacion", new { motivo = "Prueba SMTP aislada" }, HttpStatusCode.OK);
        await WaitFor(() => Task.FromResult(server.AcceptedIds.Count == 2));
        Assert.Contains(server.Attempts, message => message.Subject == "Cita cancelada - AutoCheck");
        await WaitFor(async () => await WithDb(db => db.Outbox.CountAsync(message => message.Kind.StartsWith("CITA_") && message.Completed != null)) == 2);
        Assert.False(server.CredentialsBeforeTls);
    }

    [IntegrationFact]
    public async Task PA13SmtpRejectionPreservesTheOutboxAndRetryReusesMessageIdentity()
    {
        await using var server = new LoopbackSmtpServer { RejectMessages = true };
        await using var factory = WithSmtp(server, workers: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Citizen.DefaultRequestHeaders.Authorization;
        await Post(client, "/api/pagos/intentos", new StartPaymentRequest(VehicleId, CenterId, RampId, Starts), HttpStatusCode.Created);
        Assert.True(await Dispatch(factory, "PAGO_SANDBOX"));
        Assert.True(await Dispatch(factory, "CITA_CONFIRMADA"));
        var pending = await WithDb(db => db.Outbox.SingleAsync(message => message.Kind == "CITA_CONFIRMADA"));
        Assert.Equal(1, pending.Attempts);
        Assert.Null(pending.Completed);
        Assert.Empty(server.AcceptedIds);
        server.RejectMessages = false;
        App.Clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(await Dispatch(factory, "CITA_CONFIRMADA"));
        Assert.Single(server.AcceptedIds);
        Assert.Equal(2, server.Attempts.Count);
        Assert.Single(server.Attempts.Select(message => message.MessageId).Distinct());
        Assert.NotNull(await WithDb(db => db.Outbox.Where(message => message.Id == pending.Id).Select(message => message.Completed).SingleAsync()));
        Assert.Equal(1, await WithDb(db => db.Appointments.CountAsync()));
    }

    private static async Task<bool> Dispatch(WebApplicationFactory<Program> factory, string kind)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOne(default, kind);
    }
}
