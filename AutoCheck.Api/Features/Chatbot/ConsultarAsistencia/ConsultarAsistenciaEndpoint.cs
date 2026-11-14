using System.Net.Http.Json;
using AutoCheck.Api.Features.Chatbot.Common;
using static AutoCheck.Api.Features.Chatbot.Common.AssistanceSupport;

namespace AutoCheck.Api.Features.Chatbot.ConsultarAsistencia;

public static class ConsultarAsistenciaEndpoint
{
    public static void MapConsultarAsistencia(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/asistencia", async (AssistanceRequest r, IAssistanceProvider provider, HttpContext h) =>
        {
            var question = r.Consulta?.Trim();
            if (string.IsNullOrEmpty(question) || question.Length > 2000)
            {
                return Results.Json(new AssistanceResponse("La consulta debe contener entre 1 y 2000 caracteres.", "NO_DETERMINADA", "SOLICITUD_INVALIDA"), statusCode: 400);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(h.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                var answer = await provider.Answer(question, timeout.Token).WaitAsync(timeout.Token);
                if (!Valid(answer))
                {
                    throw ApiContract.Unavailable();
                }

                return Results.Json(answer);
            }
            catch (OperationCanceledException) when (h.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ApiException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                return Results.Json(new AssistanceResponse(Contingency, "NO_DETERMINADA", "SERVICIO_NO_DISPONIBLE"), statusCode: 503);
            }
        }).RequireAuthorization().RequireRateLimiting("public").Interactive().WithTags("Asistencia").WithName("ConsultarAsistencia").WithSummary("RF-CHAT-01/02 / IS-05 / PA-09").Produces<AssistanceResponse>().Produces<AssistanceResponse>(400).Produces<AssistanceResponse>(503);
    }
}

public sealed record AssistanceRequest(string Consulta);
