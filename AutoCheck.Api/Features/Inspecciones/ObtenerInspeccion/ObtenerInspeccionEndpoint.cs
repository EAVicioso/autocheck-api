using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.ObtenerInspeccion;

public static class ObtenerInspeccionEndpoint
{
    public static void MapObtenerInspeccion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var i = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!CanRead(actor, i))
            {
                AppActor.Deny();
            }

            return Response(i);
        }).Document<InspectionResponse>("ObtenerInspeccion", "CU-05 / CU-10");
    }
}
