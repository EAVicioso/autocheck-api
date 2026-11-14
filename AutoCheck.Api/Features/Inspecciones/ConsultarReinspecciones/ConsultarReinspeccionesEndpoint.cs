using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.ConsultarReinspecciones;

public static class ConsultarReinspeccionesEndpoint
{
    public static void MapConsultarReinspecciones(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/reinspecciones", async (Guid id, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            var original = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!CanRead(a, original))
            {
                AppActor.Deny();
            }

            return new
            {
                items = (await db.Inspections.AsNoTracking().Where(x => x.PreviousId == id && (x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || a.CenterId == x.CenterId)))).OrderBy(x => x.Started).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
            };
        }).Document<ItemsResponse<InspectionResponse>>("ConsultarReinspecciones", "Arquitectura DF-05 / CU-10");
    }
}
