using AutoCheck.Api.Features.Chatbot;
using AutoCheck.Api.Features.Chatbot.Common;

namespace AutoCheck.Api.Features.Chatbot.Common;

public static class AssistanceSupport
{
    public const string Contingency = "El servicio de asistencia no está disponible temporalmente. Intenta de nuevo.";
    public static bool Valid(AssistanceResponse r) => !string.IsNullOrWhiteSpace(r.Respuesta) && r.Respuesta.Length <= 4000 && new[]
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
