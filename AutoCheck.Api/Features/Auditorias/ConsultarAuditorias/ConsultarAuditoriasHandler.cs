using AutoCheck.Api.Features.Auditorias.Common;

namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public interface IData
{
    Task<PageResponse<AuditResponse>> Read(Query query, PageSettings settings, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<PageResponse<AuditResponse>> Execute(Query query, CancellationToken ct)
    {
        actor.RequirePermission(Permissions.Audit);
        if (actor.CenterId is not null)
        {
            AppActor.Deny();
        }
        if (query.DesdeUtc > query.HastaUtc || query.Operacion?.Length > 128 || query.Entidad?.Length > 256)
        {
            throw ApiContract.Invalid("Filtros de auditoría inválidos.");
        }
        var settings = PageSettings.Create(query.Pagina, query.Tamano, query.Orden, query.Direccion, query.Cursor, "fechaUtc", true, "fechaUtc");
        return data.Read(query with { DesdeUtc = query.DesdeUtc?.ToUniversalTime(), HastaUtc = query.HastaUtc?.ToUniversalTime() }, settings, ct);
    }
}
