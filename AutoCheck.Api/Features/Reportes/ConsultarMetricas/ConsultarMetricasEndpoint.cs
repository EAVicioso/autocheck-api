using AutoCheck.Api.Features.Reportes.Common;
using static AutoCheck.Api.Features.Reportes.Common.ReportSupport;

namespace AutoCheck.Api.Features.Reportes.ConsultarMetricas;

public static class ConsultarMetricasEndpoint
{
    public static void MapConsultarMetricas(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/inspecciones", async (DateOnly? desde, DateOnly? hasta, Guid? centroId, Guid? rampaId, Guid? inspectorId, HttpContext h, AutoCheckDb db, AppActor a, TimeProvider clock, CancellationToken ct) =>
        {
            a.RequirePermission(Permissions.Audit);
            var (from, to) = Dates(desde, hasta, clock);
            if (centroId is not null)
            {
                a.RequireCenter(centroId.Value);
            }

            var q = db.Metrics.AsNoTracking().Where(x => x.Day >= from && x.Day <= to && (a.CenterId == null || a.CenterId == x.CenterId) && (centroId == null || x.CenterId == centroId) && (rampaId == null || x.RampId == rampaId) && (inspectorId == null || x.InspectorId == inspectorId));
            var totals = await q.GroupBy(_ => 1).Select(group => new { Approved = group.Sum(x => x.Approved), Rejected = group.Sum(x => x.Rejected) }).SingleOrDefaultAsync(ct);
            var approved = totals?.Approved ?? 0;
            var rejected = totals?.Rejected ?? 0;
            var total = approved + rejected;
            return new
            {
                cerradas = total,
                aprobadas = approved,
                rechazadas = rejected,
                porcentajeRechazo = total == 0 ? (double?)null : 100d * rejected / total,
                estado = total == 0 ? "Sin datos" : "Con datos",
                ultimaActualizacionUtc = await db.Aggregation.Select(x => (DateTimeOffset?)x.Updated).SingleOrDefaultAsync(ct),
                items = await q.OrderBy(x => x.Day).ThenBy(x => x.CenterId).ThenBy(x => x.RampId).ThenBy(x => x.InspectorId).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { dia = x.Day, centroId = x.CenterId, rampaId = x.RampId, inspectorId = x.InspectorId, aprobadas = x.Approved, rechazadas = x.Rejected }).ToListAsync(ct)
            };
        }).Document<MetricsResponse>("ConsultarMetricas", "RF-DAS-01/02 / PA-08a,d");
    }
}

public sealed record MetricsResponse(int Cerradas, int Aprobadas, int Rechazadas, double? PorcentajeRechazo, string Estado, DateTimeOffset? UltimaActualizacionUtc, IEnumerable<MetricRow> Items);
public sealed record MetricRow(DateOnly Dia, Guid CentroId, Guid RampaId, Guid InspectorId, int Aprobadas, int Rechazadas);
