// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot;
using AutoCheck.Api.Features.Chatbot.Common;

namespace AutoCheck.Api.Features.Chatbot.Common;

public static class AssistanceSupport
{
    public const string Contingency = "El servicio de asistencia no está disponible temporalmente. Intenta de nuevo.";
    public const string TimeoutPolicy = "Asistencia";
    public static AssistanceResponse Unavailable { get; } = new(Contingency, "NO_DETERMINADA", "SERVICIO_NO_DISPONIBLE");
    public static bool Valid(AssistanceResponse? r) => r is not null && !string.IsNullOrWhiteSpace(r.Respuesta) && r.Respuesta.Length <= 4000 && new[]
    {
        "USO_PLATAFORMA",
        "ITV",
        "FUERA_DE_ALCANCE",
        "NO_DETERMINADA"
    }.Contains(r.Categoria) && new[]
    {
        "OK",
        "INFORMACION_INSUFICIENTE",
        "FUERA_DE_ALCANCE"
    }.Contains(r.Codigo) && ((r.Categoria == "FUERA_DE_ALCANCE") == (r.Codigo == "FUERA_DE_ALCANCE")) && !(r.Categoria == "NO_DETERMINADA" && r.Codigo == "OK");
}
