// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auditorias.Common;
namespace AutoCheck.Api.Features.Auditorias.ObtenerAuditoria;

public static class ObtenerAuditoriaEndpoint
{
    public static void MapObtenerAuditoria(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/auditorias/{id:guid}", HandleAsync).RequireAuthorization("AuditoriaGlobal").WithTags("Auditoria").Document<AuditResponse>("ObtenerAuditoria", "CU-13 / PA-13");
    private static async Task<Results<Ok<AuditResponse>, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
