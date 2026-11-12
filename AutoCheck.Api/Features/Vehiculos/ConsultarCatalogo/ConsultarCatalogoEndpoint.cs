using AutoCheck.Api.Features.Vehiculos.Common;
using static AutoCheck.Api.Features.Vehiculos.Common.VehicleSupport;

namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public static class ConsultarCatalogoEndpoint
{
    public static void MapConsultarCatalogo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/catalogos/{tipo}", async (string tipo, Guid? padreId, HttpContext h, AutoCheckDb db, CancellationToken ct) =>
        {
            if (!new[]
            {
                "marcas",
                "modelos",
                "colores",
                "categorias"
            }.Contains(tipo))
            {
                throw ApiContract.Missing();
            }

            return new
            {
                items = await db.Catalog.AsNoTracking().Where(x => x.Type == tipo && (padreId == null || x.ParentId == padreId)).OrderBy(x => x.Name).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, nombre = x.Name, padreId = x.ParentId }).ToListAsync(ct)
            };
        }).RequireAuthorization().WithTags("Vehiculos").Document<ItemsResponse<CatalogResponse>>("ConsultarCatalogo", "IU-01");
    }
}

public sealed record CatalogResponse(Guid Id, string Nombre, Guid? PadreId);
