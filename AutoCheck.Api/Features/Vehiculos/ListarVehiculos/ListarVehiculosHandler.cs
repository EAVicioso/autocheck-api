// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ListarVehiculos;

public interface IData
{
    Task<PageResponse<VehicleResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public Task<PageResponse<VehicleResponse>> Execute(Query query, CancellationToken ct)
    {
        if (query.Matricula?.Length > 20)
        {
            throw ApiContract.Invalid("Matrícula demasiado larga.");
        }
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, null, "id", false, "id", "matricula");
        return data.Read(query, settings, ct);
    }
}
