using AutoCheck.Api.Features.Citas.Common;
using static AutoCheck.Api.Features.Citas.Common.AppointmentSupport;

namespace AutoCheck.Api.Features.Citas.ListarCitas;

public static class ListarCitasEndpoint
{
    public static void MapListarCitas(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (DateOnly? desde, DateOnly? hasta, string? estado, HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var (from, to) = ApiContract.UtcRange(desde, hasta);
            ApiContract.Filter(estado, "CONFIRMADA", "CANCELADA", "EN_INSPECCION", "COMPLETADA");
            return new
            {
                items = (await db.Appointments.AsNoTracking().Where(x => x.UserId == actor.Id || (actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || x.CenterId == actor.CenterId))).Where(x => (from == null || x.Starts >= from) && (to == null || x.Starts < to) && (estado == null || x.State == estado)).OrderByDescending(x => x.Starts).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
            };
        }).Document<ItemsResponse<AppointmentResponse>>("ListarCitas", "CU-03 / 6.2.1.1");
    }
}
