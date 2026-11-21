using AutoCheck.Api.Features.Auditorias.Common;
namespace AutoCheck.Api.Features.Auditorias.ObtenerAuditoria;

public interface IData
{
    Task<AuditResponse?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public async Task<AuditResponse> Execute(Query query, CancellationToken ct)
    {
        actor.RequirePermission(Permissions.Audit);
        if (actor.CenterId is not null)
        {
            AppActor.Deny();
        }
        return await data.Read(query, ct) ?? throw ApiContract.Missing();
    }
}
