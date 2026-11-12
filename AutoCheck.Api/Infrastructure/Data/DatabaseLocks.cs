using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public static class DatabaseLocks
{
    // Transaction-scoped, shared across API replicas. Locks are never process-local.
    public static Task LockAsync(this AutoCheckDb db, string resource, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
}
