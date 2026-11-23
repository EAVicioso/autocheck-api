// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Chatbot.Common;

public interface IGeminiQuota
{
    Task<bool> TryReserveAsync(int estimatedInputTokens, CancellationToken ct);
}

internal sealed partial class GeminiQuota(AutoCheckDb db, TimeProvider clock, ILogger<GeminiQuota> logger) : IGeminiQuota
{
    private const int RequestsPerMinute = 15;
    private const int InputTokensPerMinute = 250_000;
    private const int RequestsPerDay = 500;
    private static readonly TimeZoneInfo PacificTime = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public async Task<bool> TryReserveAsync(int estimatedInputTokens, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("gemini:assistance-quota", ct);

        var now = clock.GetUtcNow();
        var minuteStart = now.AddMinutes(-1);
        var pacificMidnight = TimeZoneInfo.ConvertTime(now, PacificTime).Date;
        var dayStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(pacificMidnight, PacificTime));
        var windowStart = minuteStart < dayStart ? minuteStart : dayStart;

        var calls = db.Set<GeminiQuotaReservation>();
        var usage = await calls.Where(call => call.ReservedAt >= windowStart).GroupBy(_ => 1)
            .Select(window => new
            {
                MinuteRequests = window.Count(call => call.ReservedAt > minuteStart),
                MinuteTokens = window.Sum(call => call.ReservedAt > minuteStart ? call.EstimatedInputTokens : 0),
                DayRequests = window.Count(call => call.ReservedAt >= dayStart)
            })
            .SingleOrDefaultAsync(ct);

        var reason = usage switch
        {
            { DayRequests: >= RequestsPerDay } => "requests_per_day",
            { MinuteRequests: >= RequestsPerMinute } => "requests_per_minute",
            _ when (usage?.MinuteTokens ?? 0) + estimatedInputTokens > InputTokensPerMinute => "input_tokens_per_minute",
            _ => null
        };
        if (reason is not null)
        {
            QuotaExhausted(logger, reason);
            return false;
        }

        // Count reservations even if the subsequent network outcome is uncertain.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH expired AS (
                DELETE FROM autocheck."GeminiQuotaReservation"
                WHERE "ReservedAt" < {dayStart} AND "ReservedAt" <= {minuteStart}
            )
            INSERT INTO autocheck."GeminiQuotaReservation" ("Id", "ReservedAt", "EstimatedInputTokens")
            VALUES ({Guid.NewGuid()}, {now}, {estimatedInputTokens})
            """, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Gemini quota exhausted ({Reason})")]
    private static partial void QuotaExhausted(ILogger logger, string reason);
}

internal sealed class GeminiQuotaReservation
{
    public Guid Id { get; set; }
    public DateTimeOffset ReservedAt { get; set; }
    public int EstimatedInputTokens { get; set; }
}
