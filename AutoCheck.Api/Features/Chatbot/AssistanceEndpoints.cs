// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot.ConsultarAsistencia;

namespace AutoCheck.Api.Features.Chatbot;

public static class AssistanceEndpoints
{
    public static void MapAssistance(this IEndpointRouteBuilder routes)
    {
        routes.MapConsultarAsistencia();
    }
}
