// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auditorias.Common;

namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public sealed class Data(AutoCheckDb db) : IData
{
    public async Task<PageResponse<AuditResponse>> Read(Query query, PageSettings settings, CancellationToken ct)
    {
        var q = db.Audit.AsNoTracking().Where(x => (query.DesdeUtc == null || x.At >= query.DesdeUtc) && (query.HastaUtc == null || x.At <= query.HastaUtc)
            && (query.Operacion == null || x.Operation == query.Operacion) && (query.Entidad == null || x.Entity == query.Entidad) && (query.ActorId == null || x.ActorId == query.ActorId));
        q = Seek(q, settings);
        var ordered = settings.Descending ? q.OrderByDescending(x => x.At).ThenByDescending(x => x.Id) : q.OrderBy(x => x.At).ThenBy(x => x.Id);
        var rows = await ordered.Skip(settings.Cursor == null ? (settings.Page - 1) * settings.Size : 0).Take(settings.Size + 1)
            .Select(x => new { x.Id, x.ActorId, x.At, x.Operation, x.Entity, x.Outcome, x.Correlation, x.Ip, x.Before, x.After }).ToArrayAsync(ct);
        var items = rows.Take(settings.Size).Select(x => new AuditResponse(x.Id, x.ActorId, x.At, x.Operation, x.Entity, x.Outcome, x.Correlation, x.Ip?.ToString(), Snapshot(x.Before), Snapshot(x.After))).ToArray();
        return new(items.ToImmutableArray(), settings.Cursor == null ? settings.Page : null, rows.Length > settings.Size ? settings.Encode(items[^1].FechaUtc, items[^1].Id) : null);
    }
    private static JsonElement? Snapshot(string? value)
    {
        if (value is null)
        {
            return null;
        }
        using var json = JsonDocument.Parse(value);
        return json.RootElement.Clone();
    }
    private static IQueryable<AuditEvent> Seek(IQueryable<AuditEvent> q, PageSettings settings)
    {
        if (settings.Cursor is not { } seek)
        {
            return q;
        }
        return settings.Descending ? q.Where(x => x.At < seek.Fecha || (x.At == seek.Fecha && x.Id.CompareTo(seek.Id) < 0)) : q.Where(x => x.At > seek.Fecha || (x.At == seek.Fecha && x.Id.CompareTo(seek.Id) > 0));
    }
}
