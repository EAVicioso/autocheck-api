using System.Net;
using System.Net.Sockets;
using System.Text;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Tests.Integration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoCheck.Tests.Infrastructure;

public sealed class SmtpDeliveryTests
{
    internal static SmtpOptions Settings(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        Username = LoopbackSmtpServer.Username,
        Password = LoopbackSmtpServer.Password,
        From = "autocheck@example.test",
        SenderName = "AutoCheck"
    };

    [Fact]
    public async Task DiagnosticAuthenticatesOverTlsWithoutSendingEmail()
    {
        await using var server = new LoopbackSmtpServer();
        using var client = server.TrustedClient();
        await new SmtpDelivery(client, Options.Create(Settings(server.Port))).Check(default);
        Assert.Equal(1, server.Authentications);
        Assert.False(server.CredentialsBeforeTls);
        Assert.Empty(server.Attempts);
    }

    [Fact]
    public async Task MissingStartTlsNeverTransmitsCredentialsOrEmail()
    {
        await using var server = new LoopbackSmtpServer { AdvertiseTls = false };
        using var client = server.TrustedClient();
        await Assert.ThrowsAsync<NotSupportedException>(() => new SmtpDelivery(client, Options.Create(Settings(server.Port))).Check(default));
        Assert.Equal(0, server.Authentications);
        Assert.False(server.CredentialsBeforeTls);
        Assert.Empty(server.Attempts);
    }

    [Theory]
    [InlineData("AUTH PLAIN invalid")]
    [InlineData("MAIL FROM:<autocheck@example.test>")]
    [InlineData("DATA")]
    public async Task PlaintextCommandsCannotBypassTlsNegotiation(string command)
    {
        await using var server = new LoopbackSmtpServer();
        using var connection = new TcpClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await connection.ConnectAsync(IPAddress.Loopback, server.Port, deadline.Token);
        using var network = connection.GetStream();
        using var reader = new StreamReader(network, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(network, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        Assert.StartsWith("220 ", await reader.ReadLineAsync(deadline.Token) ?? "");
        await writer.WriteLineAsync(command.AsMemory(), deadline.Token);
        Assert.StartsWith("530 ", await reader.ReadLineAsync(deadline.Token) ?? "");
        Assert.Equal(0, server.Authentications);
        Assert.Empty(server.Attempts);
        Assert.Empty(server.AcceptedIds);
    }

    [Fact]
    public async Task TlsWithoutSmtpAuthenticationCannotDeliverEmail()
    {
        await using var server = new LoopbackSmtpServer();
        using var client = server.TrustedClient();
        await client.ConnectAsync("127.0.0.1", server.Port, SecureSocketOptions.StartTls);
        using var message = new MimeMessage
        {
            Subject = "Isolated authentication test",
            Body = new TextPart("plain") { Text = "Must not be accepted before SMTP authentication." }
        };
        message.From.Add(MailboxAddress.Parse("autocheck@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        var error = await Assert.ThrowsAsync<SmtpServiceNotAuthenticatedException>(() => client.SendAsync(new SmtpSendRequest(message)));
        Assert.Equal(SmtpStatusCode.AuthenticationRequired, error.StatusCode);
        Assert.Equal(0, server.Authentications);
        Assert.Empty(server.Attempts);
        Assert.Empty(server.AcceptedIds);
    }

    [Fact]
    public async Task DefaultClientRejectsAnUntrustedServerCertificate()
    {
        await using var server = new LoopbackSmtpServer();
        using var client = new SmtpClient();
        await Assert.ThrowsAsync<SslHandshakeException>(() => new SmtpDelivery(client, Options.Create(Settings(server.Port))).Check(default));
        Assert.Equal(0, server.Authentications);
        Assert.Empty(server.Attempts);
    }

    [Fact]
    public async Task AnUnresponsiveServerCannotKeepADeliveryOpenIndefinitely()
    {
        await using var server = new LoopbackSmtpServer { StallNoOp = true };
        using var client = server.TrustedClient();
        var diagnostic = new SmtpDelivery(client, Options.Create(Settings(server.Port))).Check(default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => diagnostic.WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Empty(server.AcceptedIds);
    }

    [Theory]
    [InlineData("AutoCheck\r\nBcc: otro@example.test")]
    [InlineData("")]
    public void InvalidConfigurationFailsWithoutExposingThePassword(string senderName)
    {
        var options = Settings(587);
        options.SenderName = senderName;
        var error = Assert.Throws<ApiException>(() => options.Sender());
        Assert.Equal(503, error.Status);
        Assert.DoesNotContain(LoopbackSmtpServer.Password, error.Message);
    }

    [Fact]
    public void OpportunisticOrPlaintextTransportCannotBeConfigured()
    {
        var options = Settings(587);
        options.Transport = (SmtpTransport)99;
        Assert.Throws<ApiException>(() => options.SocketOptions);
        options.Transport = SmtpTransport.SslOnConnect;
        Assert.Equal(SecureSocketOptions.SslOnConnect, options.SocketOptions);
    }
}
