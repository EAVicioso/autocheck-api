using AutoCheck.Api.Features.Reportes.ConsultarAlertas;
using AutoCheck.Api.Features.Reportes.ConsultarMetricas;

namespace AutoCheck.Api.Features.Reportes;

public static class ReportEndpoints
{
    public static void MapReports(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/reportes").WithTags("Reportes").RequireAuthorization();
        g.MapConsultarMetricas();
        g.MapConsultarAlertas();
    }
}
