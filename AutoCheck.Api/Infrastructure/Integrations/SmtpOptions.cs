using MailKit.Security;
using MimeKit;

namespace AutoCheck.Api.Infrastructure;

public enum SmtpTransport
{
    StartTls,
    SslOnConnect
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public SmtpTransport Transport { get; set; } = SmtpTransport.StartTls;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string SenderName { get; set; } = "AutoCheck";

    public SecureSocketOptions SocketOptions => Transport switch
    {
        SmtpTransport.StartTls => SecureSocketOptions.StartTls,
        SmtpTransport.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => throw ApiContract.Unavailable()
    };

    public MailboxAddress Sender()
    {
        if (Uri.CheckHostName(Host) == UriHostNameType.Unknown || Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(SenderName) || SenderName.Length > 100 || SenderName.Any(char.IsControl) ||
            !MailboxAddress.TryParse(From, out var sender) || sender.Address != From || !From.Contains('@'))
        {
            throw ApiContract.Unavailable();
        }
        return new MailboxAddress(SenderName, sender.Address);
    }
}
