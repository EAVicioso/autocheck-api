using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoCheck.Api.Infrastructure;

public interface IAppointmentNotifications
{
    Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct);
}

public sealed class AppointmentNotifications(AutoCheckDb db, IOptions<SmtpOptions> options, SmtpDelivery delivery) : IAppointmentNotifications
{
    public async Task Send(Appointment appointment, string kind, Guid deliveryId, CancellationToken ct)
    {
        var sender = options.Value.Sender();
        var subject = kind switch
        {
            "CITA_CONFIRMADA" => "Cita confirmada - AutoCheck",
            "CITA_CANCELADA" => "Cita cancelada - AutoCheck",
            _ => throw new InvalidOperationException("Unknown appointment notification type.")
        };
        var email = await db.Users.Where(x => x.Id == appointment.UserId).Select(x => x.Email).SingleAsync(ct);
        using var message = new MimeMessage
        {
            MessageId = $"{deliveryId:N}@{sender.Address.Split('@')[1]}",
            Subject = subject,
            Body = new TextPart("plain") { Text = $"Cita {appointment.Id}. Fecha local: {TimeZoneInfo.ConvertTime(appointment.Starts, DominicanTime.Zone):yyyy-MM-dd HH:mm}. Operación: {kind}. Pago simulado, sin movimientos reales." }
        };
        message.From.Add(sender);
        message.To.Add(MailboxAddress.Parse(email));
        message.Headers.Add("X-AutoCheck-Delivery-Id", deliveryId.ToString());
        await delivery.Send(message, ct);
    }
}
