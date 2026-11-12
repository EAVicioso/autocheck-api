using System.Net.Http.Headers;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AutoCheck.Api.Infrastructure;

public interface IAppointmentNotifications
{
    Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct);
}

public sealed class AppointmentNotifications(AutoCheckDb db, IConfiguration config, IHttpClientFactory factory) : IAppointmentNotifications
{
    public async Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct)
    {
        var key = config["SendGrid:ApiKey"];
        var sender = config["SendGrid:From"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender))
        {
            throw ApiContract.Unavailable();
        }

        var email = await db.Users.Where(x => x.Id == appointment.UserId).Select(x => x.Email).SingleAsync(ct);
        var message = MailHelper.CreateSingleEmail(new EmailAddress(sender, "AutoCheck"), new EmailAddress(email), kind == "CITA_CONFIRMADA" ? "Cita confirmada - AutoCheck" : "Cita cancelada - AutoCheck", $"Cita {appointment.Id}. Fecha local: {TimeZoneInfo.ConvertTime(appointment.Starts, DominicanTime.Zone):yyyy-MM-dd HH:mm}. Operación: {kind}. Pago simulado, sin movimientos reales.", null);
        message.AddCustomArg("autocheckDeliveryId", deliveryId.ToString());
        using var http = factory.CreateClient("sendgrid");
        var client = new SendGridClient(http, new SendGridClientOptions { ApiKey = key });
        var response = await client.SendEmailAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw ApiContract.Unavailable();
        }
    }
}
