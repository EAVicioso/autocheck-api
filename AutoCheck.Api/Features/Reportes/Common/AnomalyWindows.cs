namespace AutoCheck.Api.Features.Reportes.Common;

public static class AnomalyWindows
{
    public static IEnumerable<AnomalyAlert> Build(IEnumerable<Inspection> source)
    {
        foreach (var group in source.Where(x => x.Closed is not null).GroupBy(x => new { x.InspectorId, x.CenterId }))
        {
            var sorted = group.OrderBy(x => x.Closed).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal).ToArray();
            for (var end = 20; end < sorted.Length; end++)
            {
                var window = sorted[(end - 20)..(end + 1)];
                var approved = window.Count(x => x.Result == "Aprobado");
                if (approved < 20)
                {
                    continue;
                }

                yield return new AnomalyAlert
                {
                    InspectorId = group.Key.InspectorId,
                    CenterId = group.Key.CenterId,
                    FinalInspectionId = sorted[end].Id,
                    Day = DominicanTime.Date(sorted[end].Closed!.Value),
                    From = window[0].Closed!.Value,
                    To = window[^1].Closed!.Value,
                    Approved = approved,
                    InspectionIds = window.Select(x => x.Id).ToArray(),
                    RampIds = window.Select(x => x.RampId).Distinct().ToArray()
                };
            }
        }
    }
}
