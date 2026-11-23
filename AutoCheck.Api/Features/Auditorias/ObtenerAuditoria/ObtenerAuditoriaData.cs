// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auditorias.Common;
namespace AutoCheck.Api.Features.Auditorias.ObtenerAuditoria;

public sealed class Data(AutoCheckDb db) : IData
{
    public async Task<AuditResponse?> Read(Query query, CancellationToken ct)
    {
        var x = await db.Audit.AsNoTracking().Where(x => x.Id == query.Id).Select(x => new { x.Id, x.ActorId, x.At, x.Operation, x.Entity, x.Outcome, x.Correlation, x.Ip, x.Before, x.After }).SingleOrDefaultAsync(ct);
        return x is null ? null : new AuditResponse(x.Id, x.ActorId, x.At, x.Operation, x.Entity, x.Outcome, x.Correlation, x.Ip?.ToString(), AuditJson.Read(x.Before), AuditJson.Read(x.After));
    }
}
