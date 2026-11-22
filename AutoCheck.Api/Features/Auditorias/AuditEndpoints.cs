// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

namespace AutoCheck.Api.Features.Auditorias;

public static class AuditEndpoints
{
    public static void MapAudit(this IEndpointRouteBuilder routes)
    {
        routes.MapConsultarAuditorias();
        AutoCheck.Api.Features.Auditorias.ObtenerAuditoria.ObtenerAuditoriaEndpoint.MapObtenerAuditoria(routes);
    }
}
