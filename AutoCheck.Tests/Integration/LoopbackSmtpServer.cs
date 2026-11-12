using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;

namespace AutoCheck.Tests.Integration;

// A disposable protocol fixture, bound only to loopback. It never relays email.
internal sealed class LoopbackSmtpServer : IAsyncDisposable
{
    public const string Username = "isolated-smtp-user";
    public const string Password = "isolated-smtp-password";
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly X509Certificate2 certificate;
    private readonly Task worker;
    public bool AdvertiseTls { get; init; } = true;
    public bool RejectMessages { get; set; }
    public bool StallNoOp { get; init; }
    public bool CredentialsBeforeTls { get; private set; }
    public int Authentications { get; private set; }
    public ConcurrentQueue<MimeMessage> Attempts { get; } = new();
    public ConcurrentQueue<string> AcceptedIds { get; } = new();
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public LoopbackSmtpServer()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
        listener.Start();
        worker = AcceptConnections();
    }

    public SmtpClient TrustedClient() => new()
    {
        // Trust only this fixture's exact ephemeral certificate, without changing
        // the OS trust store or the production client's validation settings.
        ServerCertificateValidationCallback = (_, presented, _, _) => presented is not null && presented.GetRawCertData().AsSpan().SequenceEqual(certificate.RawData)
    };

    private async Task AcceptConnections()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                using var connection = await listener.AcceptTcpClientAsync(stopping.Token);
                try
                {
                    await Serve(connection, stopping.Token);
                }
                catch (Exception exception) when (exception is IOException or AuthenticationException)
                {
                    // Negative TLS tests deliberately disconnect during the handshake.
                    connection.Close();
                }
            }
        }
        catch (Exception exception) when (stopping.IsCancellationRequested && exception is OperationCanceledException or SocketException)
        {
            listener.Stop();
        }
    }

    private async Task Serve(TcpClient connection, CancellationToken ct)
    {
        using var network = connection.GetStream();
        using var tls = new SslStream(network, leaveInnerStreamOpen: true);
        var wire = new SmtpWire(network);
        var secured = false;
        try
        {
            await wire.Reply("220 localhost isolated SMTP", ct);
            while (await wire.Reader.ReadLineAsync(ct) is { } line)
            {
                var command = line.Split(' ', 2)[0];
                if (command == "STARTTLS")
                {
                    await wire.Reply("220 Ready for TLS", ct);
                    wire.Dispose();
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, ct);
                    wire = new SmtpWire(tls);
                    secured = true;
                }
                else if (!await Handle(command, line, wire, secured, ct))
                {
                    break;
                }
            }
        }
        finally
        {
            wire.Dispose();
        }
    }

    private async Task<bool> Handle(string command, string line, SmtpWire wire, bool secured, CancellationToken ct)
    {
        switch (command)
        {
            case "EHLO":
                await wire.Reply(!secured && AdvertiseTls ? "250-localhost\r\n250-STARTTLS\r\n250 AUTH PLAIN" : "250-localhost\r\n250 AUTH PLAIN", ct);
                break;
            case "AUTH":
                CredentialsBeforeTls |= !secured;
                var expected = "AUTH PLAIN " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{Username}\0{Password}"));
                var valid = secured && line == expected;
                Authentications += valid ? 1 : 0;
                await wire.Reply(valid ? "235 Authenticated" : "535 Invalid credentials", ct);
                break;
            case "DATA":
                await Receive(wire, ct);
                break;
            case "QUIT":
                await wire.Reply("221 Bye", ct);
                return false;
            case "MAIL":
            case "RCPT":
            case "RSET":
                await wire.Reply("250 OK", ct);
                break;
            case "NOOP":
                if (StallNoOp)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                await wire.Reply("250 OK", ct);
                break;
            default:
                await wire.Reply("500 Unsupported command", ct);
                break;
        }
        return true;
    }

    private async Task Receive(SmtpWire wire, CancellationToken ct)
    {
        await wire.Reply("354 End with a dot", ct);
        var raw = new StringBuilder();
        while (await wire.Reader.ReadLineAsync(ct) is { } line && line != ".")
        {
            raw.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line).Append("\r\n");
            if (raw.Length > 512 * 1024)
            {
                throw new IOException("Isolated SMTP message exceeds its limit.");
            }
        }
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(raw.ToString()));
        var message = await MimeMessage.LoadAsync(bytes, ct);
        Attempts.Enqueue(message);
        if (!RejectMessages)
        {
            AcceptedIds.Enqueue(message.MessageId ?? throw new InvalidDataException("A test delivery must have a stable message id."));
        }
        await wire.Reply(RejectMessages ? "451 Try again later" : "250 Accepted", ct);
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        listener.Stop();
        await worker;
        foreach (var message in Attempts)
        {
            message.Dispose();
        }
        certificate.Dispose();
        stopping.Dispose();
    }

    private sealed class SmtpWire(Stream stream) : IDisposable
    {
        public StreamReader Reader { get; } = new(stream, Encoding.UTF8, leaveOpen: true);
        private readonly StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        public Task Reply(string value, CancellationToken ct) => writer.WriteLineAsync(value.AsMemory(), ct);
        public void Dispose()
        {
            Reader.Dispose();
            writer.Dispose();
        }
    }
}
