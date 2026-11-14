using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var error = ex switch
            {
                ApiException known => known,
                DbUpdateConcurrencyException => ApiContract.Conflict("VERSION_DESACTUALIZADA", "El registro cambió. Actualiza y reintenta."),
                DbUpdateException d when d.InnerException is Npgsql.PostgresException { SqlState: "23505" } => ApiContract.Conflict("REGISTRO_DUPLICADO", "El registro ya existe."),
                BadHttpRequestException => ApiContract.Invalid("JSON o solicitud inválidos."),
                HttpRequestException or TaskCanceledException or Npgsql.NpgsqlException => ApiContract.Unavailable(),
                _ => new ApiException(500, "ERROR_INTERNO", "No se pudo completar la operación.")
            };
            ApiLog.RequestFailed(logger, context.TraceIdentifier, error.Code, ex.GetType().Name);
            context.Response.Clear();
            if (context.Request.Path == "/api/asistencia")
            {
                await Results.Json(new { respuesta = error.Status == 503 ? AutoCheck.Api.Features.Chatbot.Common.AssistanceSupport.Contingency : error.Message, categoria = "NO_DETERMINADA", codigo = error.Status == 503 ? "SERVICIO_NO_DISPONIBLE" : "SOLICITUD_INVALIDA" }, statusCode: error.Status).ExecuteAsync(context);
                return;
            }

            await Results.Problem(statusCode: error.Status, title: error.Message, extensions: new Dictionary<string, object?> { ["codigo"] = error.Code, ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }
    }
}
