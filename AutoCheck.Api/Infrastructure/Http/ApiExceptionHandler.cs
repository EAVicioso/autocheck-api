// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Diagnostics;
namespace AutoCheck.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted || (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested))
        {
            return false;
        }
        var error = exception switch
        {
            ApiException known => known,
            DbUpdateConcurrencyException => ApiContract.Conflict("VERSION_DESACTUALIZADA", "El registro cambió. Actualiza y reintenta."),
            DbUpdateException d when d.InnerException is Npgsql.PostgresException { SqlState: "23505" } => ApiContract.Conflict("REGISTRO_DUPLICADO", "El registro ya existe."),
            DbUpdateException { InnerException: Npgsql.NpgsqlException { IsTransient: true } or TimeoutException } => ApiContract.Unavailable(),
            BadHttpRequestException bad => new ApiException(bad.StatusCode, ApiErrorCode.SolicitudInvalida, "JSON o solicitud inválidos."),
            HttpRequestException or TaskCanceledException or TimeoutException or Npgsql.NpgsqlException => ApiContract.Unavailable(),
            _ => new ApiException(500, "ERROR_INTERNO", "No se pudo completar la operación.")
        };
        ApiLog.RequestFailed(logger, httpContext.TraceIdentifier, error.Code, exception.GetType().Name);
        httpContext.Response.Clear();
        if (httpContext.Request.Path == "/api/asistencia")
        {
            await Results.Json(new { respuesta = error.Status == 503 ? AutoCheck.Api.Features.Chatbot.Common.AssistanceSupport.Contingency : error.Message, categoria = "NO_DETERMINADA", codigo = error.Status == 503 ? "SERVICIO_NO_DISPONIBLE" : "SOLICITUD_INVALIDA" }, statusCode: error.Status).ExecuteAsync(httpContext);
            return true;
        }
        await TypedResults.Problem(statusCode: error.Status, title: error.Message, extensions: new Dictionary<string, object?> { ["codigo"] = error.Code, ["traceId"] = httpContext.TraceIdentifier }).ExecuteAsync(httpContext);
        return true;
    }
}
