// AutoCheck / INTEC, SRS 1.1. Acceptance evidence; history: Git.
using AutoCheck.Api.Features.Inspecciones;
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
    public void PA05a_V1_verdict_matches_approved_SRS(int l, int md, int g, string expected) => Assert.Equal(expected, InspectionRules.Verdict(l, md, g, 3));
    [Fact] public void PA05b_Rule_version_changes_only_new_inspections() { Assert.Equal("Rechazado", InspectionRules.Verdict(4, 0, 0, 3)); Assert.Equal("Aprobado", InspectionRules.Verdict(4, 0, 0, 4)); }
    [Theory] [InlineData(-1, 0, 0, 3)] [InlineData(0, -1, 0, 3)] [InlineData(0, 0, -1, 3)] [InlineData(0, 0, 0, -1)]
    public void PA05c_Rejects_negative_counts(int l, int md, int g, int m) => Assert.Throws<ApiException>(() => InspectionRules.Verdict(l, md, g, m));
    [Fact] public void PA05_Counts_are_derived_from_defects() => Assert.Equal(new DefectCounts(3, 1, 0), InspectionRules.Count([new("Luces", "Leve", 3), new("Suspensión", "Mediano")]));
}
