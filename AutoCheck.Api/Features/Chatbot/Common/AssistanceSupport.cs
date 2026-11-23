// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot;
using AutoCheck.Api.Features.Chatbot.Common;

namespace AutoCheck.Api.Features.Chatbot.Common;

public static class AssistanceSupport
{
    public const string Contingency = "El servicio de asistencia no está disponible temporalmente. Intenta de nuevo.";
    public const string TimeoutPolicy = "Asistencia";
    public static AssistanceResponse Unavailable { get; } = new(Contingency, "NO_DETERMINADA", "SERVICIO_NO_DISPONIBLE");
    internal static ImmutableArray<string> Categories { get; } = ["USO_PLATAFORMA", "ITV", "FUERA_DE_ALCANCE", "NO_DETERMINADA"];
    internal static ImmutableArray<string> Codes { get; } = ["OK", "INFORMACION_INSUFICIENTE", "FUERA_DE_ALCANCE"];

    public static bool Valid(AssistanceResponse? r) => r is not null && !string.IsNullOrWhiteSpace(r.Respuesta) && r.Respuesta.Length <= 4000
        && Categories.Contains(r.Categoria) && Codes.Contains(r.Codigo)
        && ((r.Categoria == "FUERA_DE_ALCANCE") == (r.Codigo == "FUERA_DE_ALCANCE"))
        && !(r.Categoria == "NO_DETERMINADA" && r.Codigo == "OK");
}
