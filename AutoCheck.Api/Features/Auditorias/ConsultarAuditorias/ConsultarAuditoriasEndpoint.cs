namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public static class ConsultarAuditoriasEndpoint
{
    public static void MapConsultarAuditorias(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auditorias", async (DateTimeOffset? desdeUtc, DateTimeOffset? hastaUtc, string? operacion, string? entidad, Guid? actorId, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            a.RequirePermission(Permissions.Audit);
            // Events may contain cross-center security operations; their consultation is global only.
            if (a.CenterId is not null)
            {
                AppActor.Deny();
            }

            var from = desdeUtc?.ToUniversalTime();
            var to = hastaUtc?.ToUniversalTime();
            if (from > to)
            {
                throw ApiContract.Invalid("Rango de fechas inválido.");
            }

            return new
            {
                items = await db.Audit.AsNoTracking().Where(x => (from == null || x.At >= from) && (to == null || x.At <= to) && (operacion == null || x.Operation == operacion) && (entidad == null || x.Entity == entidad) && (actorId == null || x.ActorId == actorId)).OrderByDescending(x => x.At).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, actorId = x.ActorId, fechaUtc = x.At, operacion = x.Operation, entidad = x.Entity, resultado = x.Outcome, correlacion = x.Correlation }).ToListAsync(ct)
            };
        }).RequireAuthorization().WithTags("Auditoria").Document<ItemsResponse<AuditResponse>>("ConsultarAuditorias", "CU-13 / PA-13a,b");
    }
}

public sealed record AuditResponse(Guid Id, Guid? ActorId, DateTimeOffset FechaUtc, string Operacion, string Entidad, string Resultado, string Correlacion);
