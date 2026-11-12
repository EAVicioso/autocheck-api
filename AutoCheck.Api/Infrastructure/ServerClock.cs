// AutoCheck SRS 1.1. PostgreSQL stores timestamps at microsecond precision.
namespace AutoCheck.Api.Infrastructure;

public sealed class ServerClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        var utc = DateTimeOffset.UtcNow;
        return new DateTimeOffset(utc.UtcTicks - utc.UtcTicks % 10, TimeSpan.Zero);
    }
}
