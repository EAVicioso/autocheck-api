// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using AutoCheck.Api.Features.Reportes;
using AutoCheck.Api.Features.Reportes.Common;
using AutoCheck.Api.Infrastructure;
using AutoCheck.Api.Infrastructure.Data;

namespace AutoCheck.Tests.Features.Reportes;

public class ReportTests
{
    [Theory]
    [InlineData(20, 20, 0)]
    [InlineData(21, 19, 0)]
    [InlineData(21, 20, 1)]
    [InlineData(21, 21, 1)]
    public void PA08bUsesFull21WindowWithoutRounding(int total, int approved, int alerts)
    {
        var rows = Rows(total, approved);
        Assert.Equal(alerts, AnomalyWindows.Build(rows).Count());
    }

    [Fact]
    public void PA08cWindowsCrossMidnightAndRampsButNeverInspectors()
    {
        var rows = Rows(22, 22);
        rows[0].RampId = Guid.NewGuid();
        var windows = AnomalyWindows.Build(rows).ToList();
        Assert.Equal(2, windows.Count);
        Assert.All(windows, w => Assert.Equal(21, w.InspectionIds.Length));
        Assert.Equal(2, windows[0].RampIds.Length);
        rows[^1].InspectorId = Guid.NewGuid();
        Assert.Single(AnomalyWindows.Build(rows));
    }

    [Fact]
    public void PA08aLocalMidnightUsesHalfOpenDayBoundary()
    {
        Assert.Equal(new DateOnly(2026, 11, 11), DominicanTime.Date(DateTimeOffset.Parse("2026-11-12T03:59:59Z", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(new DateOnly(2026, 11, 12), DominicanTime.Date(DateTimeOffset.Parse("2026-11-12T04:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static Inspection[] Rows(int count, int approved)
    {
        var inspector = Guid.NewGuid();
        var center = Guid.NewGuid();
        var ramp = Guid.NewGuid();
        return Enumerable.Range(0, count).Select(n => new Inspection { Id = Guid.NewGuid(), InspectorId = inspector, CenterId = center, RampId = ramp, Closed = DateTimeOffset.Parse("2026-11-12T03:59:50Z", System.Globalization.CultureInfo.InvariantCulture).AddSeconds(n), Result = n < approved ? "Aprobado" : "Rechazado" }).ToArray();
    }
}
