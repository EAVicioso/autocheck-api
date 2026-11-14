using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace AutoCheck.Tests.Integration;

// Faults are scoped to an isolated test host, never a production switch.
public sealed class ResetConnectionFailure : DbConnectionInterceptor
{
    public bool Offline { get; set; }
    public bool TimeoutOnce { get; set; }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Offline)
        {
            throw new NpgsqlException("Synthetic loopback database outage.");
        }
        if (TimeoutOnce)
        {
            TimeoutOnce = false;
            throw new TaskCanceledException("Synthetic database connection timeout.");
        }
        return ValueTask.FromResult(result);
    }
}
