using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
namespace AutoCheck.Tests.Infrastructure;

public sealed class LifecycleTests
{
    [Theory]
    [InlineData(0, 1, 400, "SOLICITUD_INVALIDA")]
    [InlineData(1, 2, 409, "VERSION_DESACTUALIZADA")]
    public void InvalidVersionNeverPasses(long expected, long current, int status, string code)
    {
        var error = Assert.Throws<ApiException>(() => Lifecycle.RequireVersion(expected, current, null));
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Code);
    }
    [Fact]
    public void ArchivedResourcesCannotBeMutated()
    {
        var error = Assert.Throws<ApiException>(() => Lifecycle.RequireVersion(1, 1, DateTimeOffset.UnixEpoch));
        Assert.Equal("RECURSO_ARCHIVADO", error.Code);
    }
    [Fact]
    public async Task CancelledRequestDoesNotBecomeAnInternalError()
    {
        var logger = Substitute.For<ILogger<ApiExceptionHandler>>();
        var handler = new ApiExceptionHandler(logger);
        var http = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        Assert.False(await handler.TryHandleAsync(http, new OperationCanceledException(), http.RequestAborted));
        Assert.Empty(logger.ReceivedCalls());
    }

    [Fact]
    public async Task TransientDatabaseWriteFailureUsesServiceUnavailable()
    {
        var handler = new ApiExceptionHandler(Substitute.For<ILogger<ApiExceptionHandler>>());
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream();
        http.Response.Body = body;
        var error = new DbUpdateException("database details", new NpgsqlException("provider details", new TimeoutException()));

        Assert.True(await handler.TryHandleAsync(http, error, default));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        var payload = System.Text.Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("SERVICIO_NO_DISPONIBLE", payload);
        Assert.DoesNotContain("provider details", payload);
    }

    [Theory]
    [InlineData("mongodb://remote.example.test:27017")]
    [InlineData("mongodb://remote.example.test:27017/?tls=true&tlsInsecure=true")]
    [InlineData("mongodb://127.0.0.1:27017/?w=0")]
    public void MongoArchiveRejectsInsecureOrUnacknowledgedConfiguration(string uri)
    {
        using var connection = new MongoArchiveConnection(Options.Create(new MongoDbOptions { ConnectionString = uri }));
        var error = Assert.Throws<ApiException>(() => connection.Client);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.Status);
    }

}
