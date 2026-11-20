using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
}
