namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public interface IData
{
    Task<PageResponse<HistoryResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<PageResponse<HistoryResponse>> Execute(Query query, CancellationToken ct)
    {
        if (actor.Role == Roles.Admin)
        {
            actor.RequirePermission(Permissions.Audit);
        }
        ApiContract.Filter(query.Estado, "ABIERTA", "CERRADA");
        ApiContract.Filter(query.Resultado, "Aprobado", "Rechazado");
        _ = ApiContract.UtcRange(query.Desde, query.Hasta);
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, query.Cursor, "cierreUtc", true, "cierreUtc");
        return data.Read(query, settings, ct);
    }
}
