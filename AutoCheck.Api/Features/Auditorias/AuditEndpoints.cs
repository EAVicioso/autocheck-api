using AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

namespace AutoCheck.Api.Features.Auditorias;

public static class AuditEndpoints
{
    public static void MapAudit(this IEndpointRouteBuilder routes)
    {
        routes.MapConsultarAuditorias();
    }
}
