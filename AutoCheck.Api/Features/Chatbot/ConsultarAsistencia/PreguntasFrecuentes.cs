// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot.Common;

namespace AutoCheck.Api.Features.Chatbot.ConsultarAsistencia;

internal static class PreguntasFrecuentes
{
    // Match whole questions. Ambiguous queries and requests for actions go to the provider.
    internal static AssistanceResponse? FindAnswer(string question) => question
        .Trim(' ', '¿', '?', '.').ToLowerInvariant() switch
    {
        "cómo reservar una cita" or "cómo reservo una cita" or "cómo agendar una cita" or
        "cómo puedo reservar una cita" or "cómo puedo agendar una cita" =>
            Platform("Registra tu vehículo y consulta la disponibilidad por centro y rampa. Inicia el pago simulado para el bloque elegido. La cita se confirma si el pago se aprueba dentro de la retención de cinco minutos."),
        "cuánto dura una cita" or "cuánto tiempo dura una cita" =>
            Platform("Cada cita dura treinta minutos."),
        "puedo cancelar una cita" or "cómo reagendo una cita" =>
            Platform("Puedes cancelar una cita antes de su inicio si la inspección aún no ha empezado. Para reagendar, cancela la cita y haz una nueva reserva."),
        "el pago es real" or "los pagos son reales" or "cómo funciona el pago simulado" =>
            Platform("Los pagos en AutoCheck son simulados. La cita se confirma si el pago se aprueba dentro de la retención de cinco minutos."),
        "cómo descargo mi certificado" or "cómo descargar el certificado" or "cómo puedo descargar mi certificado" =>
            Itv("El propietario puede descargar el certificado desde el historial cuando la inspección está aprobada."),
        "el certificado tiene validez oficial" or "el certificado tiene validez oficial del intrant" =>
            Itv("El certificado de AutoCheck es académico. Carece de validez oficial del INTRANT y no autoriza a circular."),
        "qué es la itv" or "qué es la inspección técnica vehicular" =>
            Itv("En AutoCheck, la inspección técnica vehicular evalúa las condiciones del vehículo mediante el registro manual de defectos leves, medianos y graves."),
        _ => null
    };

    private static AssistanceResponse Platform(string answer) => new(answer, "USO_PLATAFORMA", "OK");

    private static AssistanceResponse Itv(string answer) => new(answer, "ITV", "OK");
}
