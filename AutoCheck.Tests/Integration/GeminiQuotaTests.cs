// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoCheck.Tests.Integration;

public sealed class GeminiQuotaTests : DatabaseTest
{
    [IntegrationFact]
    public async Task PA09ConcurrentScopesShareOneRollingMinuteQuota()
    {
        var attempts = await Task.WhenAll(Enumerable.Range(0, 19).Select(_ => Reserve(5_000)));
        Assert.Equal(15, attempts.Count(allowed => allowed));

        App.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(await Reserve(5_000));
        App.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await Reserve(5_000));
    }

    [IntegrationFact]
    public async Task PA09QuotaRejectsTokensAboveTheMinuteBudget()
    {
        Assert.True(await Reserve(245_000));
        Assert.True(await Reserve(5_000));
        Assert.False(await Reserve(1));
    }

    [IntegrationFact]
    public async Task PA09DailyQuotaResetsAtPacificMidnightWithoutResettingTheRollingMinute()
    {
        // Los Angeles is UTC-07 on this date. The UTC calendar day has already changed.
        App.Clock.Now = new DateTimeOffset(2026, 7, 11, 6, 59, 30, TimeSpan.Zero);
        var priorHour = App.Clock.Now.AddHours(-1);
        await WithDb(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO autocheck."GeminiQuotaReservation" ("Id", "ReservedAt", "EstimatedInputTokens")
            SELECT gen_random_uuid(), {priorHour}, 1000 FROM generate_series(1, 485)
            """));
        for (var request = 0; request < 15; request++)
        {
            Assert.True(await Reserve(1_000));
        }

        Assert.False(await Reserve(1_000));
        App.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(await Reserve(1_000));
        App.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(await Reserve(1_000));
        Assert.Equal(1, await WithDb(db => db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM autocheck."GeminiQuotaReservation"
            """).SingleAsync()));
    }

    private async Task<bool> Reserve(int tokens)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGeminiQuota>().TryReserveAsync(tokens, default);
    }
}
