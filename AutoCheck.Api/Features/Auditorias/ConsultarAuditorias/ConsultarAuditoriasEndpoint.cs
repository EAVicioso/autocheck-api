// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auditorias.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public static class ConsultarAuditoriasEndpoint
{
    public static void MapConsultarAuditorias(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/auditorias", HandleAsync).RequireAuthorization("AuditoriaGlobal").WithTags("Auditorias").Document<PageResponse<AuditResponse>>("ConsultarAuditorias", "CU-13 / PA-13");
    private static async Task<Results<Ok<PageResponse<AuditResponse>>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync([AsParameters] Query query, Handler handler, CancellationToken ct) => TypedResults.Ok(await handler.Execute(query, ct));
}
