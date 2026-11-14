using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoCheck.Api.Infrastructure;

// One client per outbox scope: MailKit clients cannot be shared across concurrent sends.
public sealed class SmtpDelivery(ISmtpClient client, IOptions<SmtpOptions> options)
{
    public Task Send(MimeMessage message, CancellationToken ct) => Run((smtp, token) => smtp.SendAsync(new SmtpSendRequest(message), token), ct);

    // Authenticates and issues NOOP without sending email or exposing credentials.
    public Task Check(CancellationToken ct) => Run(static (smtp, token) => smtp.NoOpAsync(token), ct);

    private async Task Run(Func<ISmtpClient, CancellationToken, Task> command, CancellationToken ct)
    {
        var settings = options.Value;
        _ = settings.Sender();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        client.Timeout = 5000;
        await client.ConnectAsync(settings.Host, settings.Port, settings.SocketOptions, deadline.Token);
        await client.AuthenticateAsync(settings.Username, settings.Password, deadline.Token);
        await command(client, deadline.Token);
        // SMTP accepted the operation. A failed QUIT must not turn acceptance into a retry.
        await client.DisconnectAsync(false, CancellationToken.None);
    }
}
