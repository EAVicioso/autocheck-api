// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using AutoCheck.Api.Features.Inspecciones;
using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Infrastructure;

namespace AutoCheck.Tests.Features.Inspecciones;

public class InspectionRulesTests
{
    [Theory]
    [InlineData(0, 0, 0, "Aprobado")]
    [InlineData(3, 1, 0, "Aprobado")]
    [InlineData(4, 0, 0, "Rechazado")]
    [InlineData(0, 2, 0, "Rechazado")]
    [InlineData(0, 0, 1, "Rechazado")]
    public void PA05aV1VerdictMatchesApprovedSRS(int l, int md, int g, string expected) => Assert.Equal(expected, InspectionRules.Verdict(l, md, g, 3));
    [Fact]
    public void PA05bRuleVersionChangesOnlyNewInspections()
    {
        Assert.Equal("Rechazado", InspectionRules.Verdict(4, 0, 0, 3));
        Assert.Equal("Aprobado", InspectionRules.Verdict(4, 0, 0, 4));
    }

    [Theory]
    [InlineData(-1, 0, 0, 3)]
    [InlineData(0, -1, 0, 3)]
    [InlineData(0, 0, -1, 3)]
    [InlineData(0, 0, 0, -1)]
    public void PA05cRejectsNegativeCounts(int l, int md, int g, int m) => Assert.Throws<ApiException>(() => InspectionRules.Verdict(l, md, g, m));
    [Fact]
    public void PA05CountsAreDerivedFromDefects() => Assert.Equal(new DefectCounts(3, 1, 0), InspectionRules.Count([new("Luces", "Leve", 3), new("Suspensión", "Mediano")]));
}
