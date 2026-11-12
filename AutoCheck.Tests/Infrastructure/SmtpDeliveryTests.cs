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
