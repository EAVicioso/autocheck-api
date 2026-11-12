using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public static class ConsultarHistorialEndpoint
{
    public static void MapConsultarHistorial(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/vehiculos/{id:guid}/historial", async (Guid id, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!await VehicleSupport.CanRead(db, a, vehicle, ct))
            {
                AppActor.Deny();
            }

            return new
            {
                items = await db.Inspections.AsNoTracking().Where(x => x.VehicleId == id && x.Closed != null && (x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || x.CenterId == a.CenterId)))).OrderByDescending(x => x.Closed).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, cierreUtc = x.Closed, resultado = x.Result, versionReglas = x.RuleVersion, m = x.MaxMinor, l = x.Minor, md = x.Medium, g = x.Serious, centroId = x.CenterId, inspeccionAnteriorId = x.PreviousId }).ToListAsync(ct)
            };
        }).RequireAuthorization().WithTags("Historial").Document<ItemsResponse<HistoryResponse>>("ConsultarHistorial", "CU-10 / PA-10 / RS-08");
    }
}

public sealed record HistoryResponse(Guid Id, DateTimeOffset? CierreUtc, string? Resultado, string VersionReglas, int M, int L, int Md, int G, Guid CentroId, Guid? InspeccionAnteriorId);
