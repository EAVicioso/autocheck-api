using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace AutoCheck.Api.Infrastructure;

public static class RateLimitResponse
{
    public static ValueTask Rejected(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    }
}
