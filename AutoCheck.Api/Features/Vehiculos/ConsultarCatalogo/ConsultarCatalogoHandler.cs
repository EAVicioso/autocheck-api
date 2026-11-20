namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public interface IData
{
    Task<PageResponse<CatalogResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public Task<PageResponse<CatalogResponse>> Execute(Query query, CancellationToken ct)
    {
        if (query.Tipo is not ("marcas" or "modelos" or "colores" or "categorias"))
        {
            throw ApiContract.Missing();
        }
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, null, "nombre", false, "nombre");
        return data.Read(query, settings, ct);
    }
}
