using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ListarCentros;

public static class ListarCentrosEndpoint
{
    public static void MapListarCentros(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (HttpContext h, AutoCheckDb db, CancellationToken ct) => new { items = await db.Centers.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new CenterResponse(x.Id, x.Name, x.Address, x.Active)).ToListAsync(ct) }).Document<ItemsResponse<CenterResponse>>("ListarCentros", "CU-12 / RAE-01");
    }
}
