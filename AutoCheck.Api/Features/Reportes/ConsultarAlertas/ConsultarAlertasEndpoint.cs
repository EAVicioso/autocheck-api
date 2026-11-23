// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1; Section C / PA-05/08/09/10. Version 1.0; history: Git.
using AutoCheck.Api.Features.Reportes.Common;
using static AutoCheck.Api.Features.Reportes.Common.ReportSupport;

namespace AutoCheck.Api.Features.Reportes.ConsultarAlertas;

public static class ConsultarAlertasEndpoint
{
    public static void MapConsultarAlertas(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/alertas", async (DateOnly? desde, DateOnly? hasta, Guid? centroId, Guid? rampaId, Guid? inspectorId, HttpContext h, AutoCheckDb db, AppActor a, TimeProvider clock, CancellationToken ct) =>
        {
            a.RequirePermission(Permissions.Audit);
            var (from, to) = Dates(desde, hasta, clock);
            if (centroId is not null)
            {
                a.RequireCenter(centroId.Value);
            }

            await using var snapshot = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead, ct);
            var result = new
            {
                ultimaActualizacionUtc = await db.Aggregation.Select(x => (DateTimeOffset?)x.Updated).SingleOrDefaultAsync(ct),
                items = await db.Alerts.AsNoTracking().Where(x => x.Day >= from && x.Day <= to && (a.CenterId == null || a.CenterId == x.CenterId) && (centroId == null || x.CenterId == centroId) && (inspectorId == null || x.InspectorId == inspectorId) && (rampaId == null || x.RampIds.Contains(rampaId.Value))).OrderBy(x => x.Day).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, inspectorId = x.InspectorId, centroId = x.CenterId, inspecciones = x.InspectionIds, desdeUtc = x.From, hastaUtc = x.To, aprobadas = x.Approved, total = 21, porcentaje = 100d * x.Approved / 21, fecha = x.Day }).ToListAsync(ct)
            };
            await snapshot.CommitAsync(ct);
            return result;
        }).Document<AlertsResponse>("ConsultarAlertas", "RF-DAS-03 / PA-08b,c,d");
    }
}

public sealed record AlertResponse(Guid Id, Guid InspectorId, Guid CentroId, Guid[] Inspecciones, DateTimeOffset DesdeUtc, DateTimeOffset HastaUtc, int Aprobadas, int Total, double Porcentaje, DateOnly Fecha);
public sealed record AlertsResponse(DateTimeOffset? UltimaActualizacionUtc, IEnumerable<AlertResponse> Items);
