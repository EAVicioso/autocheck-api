// AutoCheck academic prototype, SRS 1.1; CU-08, RF-DAS-01/02/03, PA-08.
namespace AutoCheck.Api.Features.Reportes;

public sealed record MetricRow(DateOnly Dia, Guid CentroId, Guid RampaId, Guid InspectorId, int Aprobadas, int Rechazadas);
public sealed record MetricsResponse(int Cerradas, int Aprobadas, int Rechazadas, double? PorcentajeRechazo, string Estado, DateTimeOffset? UltimaActualizacionUtc, IEnumerable<MetricRow> Items);
public sealed record AlertResponse(Guid Id, Guid InspectorId, Guid CentroId, Guid[] Inspecciones, DateTimeOffset DesdeUtc, DateTimeOffset HastaUtc, int Aprobadas, int Total, double Porcentaje, DateOnly Fecha);

public static class AnomalyWindows
{
    public static IEnumerable<AnomalyAlert> Build(IEnumerable<Inspection> source)
    {
        foreach (var group in source.Where(x => x.Closed is not null).GroupBy(x => new { x.InspectorId, x.CenterId }))
        {
            var sorted = group.OrderBy(x => x.Closed).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal).ToArray();
            for (var end = 20; end < sorted.Length; end++)
            {
                var window = sorted[(end - 20)..(end + 1)]; var approved = window.Count(x => x.Result == "Aprobado");
                if (approved < 20) continue;
                yield return new AnomalyAlert { InspectorId = group.Key.InspectorId, CenterId = group.Key.CenterId, FinalInspectionId = sorted[end].Id,
                    Day = DominicanTime.Date(sorted[end].Closed!.Value), From = window[0].Closed!.Value, To = window[^1].Closed!.Value,
                    Approved = approved, InspectionIds = window.Select(x => x.Id).ToArray(), RampIds = window.Select(x => x.RampId).Distinct().ToArray() };
            }
        }
    }
}

public sealed class ReportConsolidator(AutoCheckDb db, TimeProvider clock)
{
    public async Task Run(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await db.LockAsync("reports", ct);
        var rows = await db.Inspections.AsNoTracking().Where(x => x.Closed != null)
            .Select(x => new Inspection { Id = x.Id, Closed = x.Closed, CenterId = x.CenterId, RampId = x.RampId, InspectorId = x.InspectorId, Result = x.Result }).ToListAsync(ct); var now = clock.GetUtcNow();
        await db.Metrics.ExecuteDeleteAsync(ct);
        db.Metrics.AddRange(rows.GroupBy(x => new { Day = DominicanTime.Date(x.Closed!.Value), x.CenterId, x.RampId, x.InspectorId }).Select(g => new DailyMetric
        {
            Day = g.Key.Day, CenterId = g.Key.CenterId, RampId = g.Key.RampId, InspectorId = g.Key.InspectorId, Approved = g.Count(x => x.Result == "Aprobado"), Rejected = g.Count(x => x.Result == "Rechazado"), Updated = now
        }));
        var known = (await db.Alerts.Select(x => x.FinalInspectionId).ToListAsync(ct)).ToHashSet();
        db.Alerts.AddRange(AnomalyWindows.Build(rows).Where(x => !known.Contains(x.FinalInspectionId)));
        var state = await db.Aggregation.FindAsync([1], ct); if (state is null) { state = new(); db.Aggregation.Add(state); } state.Updated = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}

public static class ReportEndpoints
{
    public static void MapReports(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/reportes").WithTags("Reportes").RequireAuthorization();
        g.MapGet("/inspecciones", async (DateOnly? desde, DateOnly? hasta, Guid? centroId, Guid? rampaId, Guid? inspectorId, HttpContext h, AutoCheckDb db, AppActor a, TimeProvider clock, CancellationToken ct) =>
        {
            a.RequirePermission(Permissions.Audit); var (from, to) = Dates(desde, hasta, clock);
            if (centroId is not null) a.RequireCenter(centroId.Value);
            var q = db.Metrics.AsNoTracking().Where(x => x.Day >= from && x.Day <= to && (a.CenterId == null || a.CenterId == x.CenterId) &&
                (centroId == null || x.CenterId == centroId) && (rampaId == null || x.RampId == rampaId) && (inspectorId == null || x.InspectorId == inspectorId));
            var approved = await q.SumAsync(x => x.Approved, ct); var rejected = await q.SumAsync(x => x.Rejected, ct); var total = approved + rejected;
            return new { cerradas = total, aprobadas = approved, rechazadas = rejected, porcentajeRechazo = total == 0 ? (double?)null : 100d * rejected / total,
                estado = total == 0 ? "Sin datos" : "Con datos", ultimaActualizacionUtc = await db.Aggregation.Select(x => (DateTimeOffset?)x.Updated).SingleOrDefaultAsync(ct),
                items = await q.OrderBy(x => x.Day).ThenBy(x => x.CenterId).ThenBy(x => x.RampId).ThenBy(x => x.InspectorId).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h))
                    .Select(x => new { dia = x.Day, centroId = x.CenterId, rampaId = x.RampId, inspectorId = x.InspectorId, aprobadas = x.Approved, rechazadas = x.Rejected }).ToListAsync(ct) };
        }).Document<MetricsResponse>("ConsultarMetricas", "RF-DAS-01/02 / PA-08a,d");
        g.MapGet("/alertas", async (DateOnly? desde, DateOnly? hasta, Guid? centroId, Guid? rampaId, Guid? inspectorId, HttpContext h, AutoCheckDb db, AppActor a, TimeProvider clock, CancellationToken ct) =>
        {
            a.RequirePermission(Permissions.Audit); var (from, to) = Dates(desde, hasta, clock); if (centroId is not null) a.RequireCenter(centroId.Value);
            return new { items = await db.Alerts.AsNoTracking().Where(x => x.Day >= from && x.Day <= to && (a.CenterId == null || a.CenterId == x.CenterId) &&
                (centroId == null || x.CenterId == centroId) && (inspectorId == null || x.InspectorId == inspectorId) && (rampaId == null || x.RampIds.Contains(rampaId.Value)))
                .OrderBy(x => x.Day).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, inspectorId = x.InspectorId, centroId = x.CenterId,
                    inspecciones = x.InspectionIds, desdeUtc = x.From, hastaUtc = x.To, aprobadas = x.Approved, total = 21, porcentaje = 100d * x.Approved / 21, fecha = x.Day }).ToListAsync(ct) };
        }).Document<ItemsResponse<AlertResponse>>("ConsultarAlertas", "RF-DAS-03 / PA-08b,c,d");
    }
    private static (DateOnly, DateOnly) Dates(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var a = from ?? DominicanTime.Date(clock.GetUtcNow()); var b = to ?? a;
        if (b < a || b.DayNumber - a.DayNumber > 366) throw ApiContract.Invalid("Rango de fechas inválido; máximo 366 días."); return (a, b);
    }
}
