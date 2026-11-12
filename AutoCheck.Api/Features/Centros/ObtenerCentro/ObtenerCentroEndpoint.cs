using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ObtenerCentro;

public static class ObtenerCentroEndpoint
{
    public static void MapObtenerCentro(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, CancellationToken ct) => new { centro = Response(await db.Centers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing()), rampas = await db.Ramps.AsNoTracking().Where(x => x.CenterId == id).Select(x => new RampResponse(x.Id, x.CenterId, x.Name, x.Active)).ToListAsync(ct), horarios = await db.Schedules.AsNoTracking().Where(x => x.CenterId == id).OrderBy(x => x.Day).Select(x => new ScheduleEntry(x.Day, x.Opens, x.Closes)).ToListAsync(ct) }).Document<CenterDetailResponse>("ObtenerCentro", "CU-12");
    }
}

public sealed record CenterDetailResponse(CenterResponse Centro, IReadOnlyList<RampResponse> Rampas, IReadOnlyList<ScheduleEntry> Horarios);
