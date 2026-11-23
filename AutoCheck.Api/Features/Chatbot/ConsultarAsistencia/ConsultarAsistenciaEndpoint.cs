// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Chatbot.Common;
using static AutoCheck.Api.Features.Chatbot.Common.AssistanceSupport;
using Npgsql;

namespace AutoCheck.Api.Features.Chatbot.ConsultarAsistencia;

public static partial class ConsultarAsistenciaEndpoint
{
    public static void MapConsultarAsistencia(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/asistencia", async (AssistanceRequest r, IAssistanceProvider provider,
            ILoggerFactory loggerFactory, HttpContext h) =>
        {
            var question = r.Consulta?.Trim();
            if (string.IsNullOrEmpty(question) || question.Length > 2000)
            {
                return Results.Json(new AssistanceResponse("La consulta debe contener entre 1 y 2000 caracteres.", "NO_DETERMINADA", "SOLICITUD_INVALIDA"), statusCode: 400);
            }

            if (PreguntasFrecuentes.FindAnswer(question) is { } localAnswer)
            {
                return Results.Json(localAnswer);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(h.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                var answer = await provider.Answer(question, timeout.Token).WaitAsync(timeout.Token);
                if (!Valid(answer))
                {
                    throw new JsonException("Invalid assistance response values.");
                }

                return Results.Json(answer);
            }
            catch (OperationCanceledException) when (h.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException or ApiException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or NpgsqlException)
            {
                AssistanceUnavailable(loggerFactory.CreateLogger("AutoCheck.Asistencia"), FailureReason(ex));
                return Results.Json(Unavailable, statusCode: 503);
            }
        }).RequireAuthorization().RequireRateLimiting("public").Interactive().WithTags("Asistencia").WithName("ConsultarAsistencia").WithSummary("RF-CHAT-01/02 / IS-05 / PA-09").Produces<AssistanceResponse>().Produces<AssistanceResponse>(400).Produces<AssistanceResponse>(503).WithRequestTimeout(TimeoutPolicy);
    }

    private static string FailureReason(Exception error) => error switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "provider_rate_limited",
        HttpRequestException => "provider_http_error",
        TimeoutException or OperationCanceledException => "timeout",
        NpgsqlException => "quota_storage_unavailable",
        JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException => "invalid_provider_response",
        _ => "unavailable"
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistance unavailable ({Reason})")]
    private static partial void AssistanceUnavailable(ILogger logger, string reason);
}

public sealed record AssistanceRequest(string Consulta);
