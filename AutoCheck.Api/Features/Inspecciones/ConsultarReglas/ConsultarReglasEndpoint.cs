using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.ConsultarReglas;

public static class ConsultarReglasEndpoint
{
    public static void MapConsultarReglas(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", async (HttpContext h, AutoCheckDb db, CancellationToken ct) => new { items = await db.Rules.AsNoTracking().OrderByDescending(x => x.Effective).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, version = x.Version, m = x.MaxMinor, vigenciaUtc = x.Effective }).ToListAsync(ct) }).Document<ItemsResponse<RuleResponse>>("ConsultarReglas", "RAE-02");
    }
}
