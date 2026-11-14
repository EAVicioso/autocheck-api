using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.ListarInspecciones;

public static class ListarInspeccionesEndpoint
{
    public static void MapListarInspecciones(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (DateOnly? desde, DateOnly? hasta, string? estado, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            var (from, to) = ApiContract.UtcRange(desde, hasta);
            ApiContract.Filter(estado, "ABIERTA", "CERRADA");
            return new
            {
                items = (await db.Inspections.AsNoTracking().Where(x => x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || x.CenterId == a.CenterId))).Where(x => (from == null || x.Started >= from) && (to == null || x.Started < to) && (estado == null || x.State == estado)).OrderByDescending(x => x.Started).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
            };
        }).Document<ItemsResponse<InspectionResponse>>("ListarInspecciones", "CU-05 / CU-10");
    }
}
