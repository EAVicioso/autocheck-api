using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Features.Pagos.RecibirPagoSandbox;

public static class RecibirPagoSandboxEndpoint
{
    public static void MapRecibirPagoSandbox(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhook", HandleAsync).AllowAnonymous().RequireRateLimiting("auth").Accepts<SandboxEvent>("application/json").Document<PaymentStateResponse>("RecibirPagoSandbox", "IS-03 / PA-04a,c; X-Sandbox-Signature HMAC-SHA256 required");
    }

    private static async Task<ApiReply> HandleAsync(HttpContext h, IConfiguration config, Mutation mutation, PaymentWorkflow workflow)
    {
        var secret = config["Sandbox:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw ApiContract.Unavailable();
        }

        using var stream = new MemoryStream();
        await h.Request.Body.CopyToAsync(stream, h.RequestAborted);
        var rawBody = stream.ToArray();
        var body = Encoding.UTF8.GetString(rawBody).TrimStart('\uFEFF');
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody);
        var signature = h.Request.Headers["X-Sandbox-Signature"].ToString();
        if (signature.Length != 64 || !signature.All(Uri.IsHexDigit))
        {
            throw new ApiException(401, "FIRMA_INVALIDA", "Firma de evento inválida.");
        }

        var supplied = Convert.FromHexString(h.Request.Headers["X-Sandbox-Signature"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            throw new ApiException(401, "FIRMA_INVALIDA", "Firma de evento inválida.");
        }

        SandboxEvent e;
        try
        {
            e = JsonSerializer.Deserialize<SandboxEvent>(body, ApiReply.JsonOptions) ?? throw ApiContract.Invalid("Evento inválido.");
        }
        catch (JsonException)
        {
            throw ApiContract.Invalid("Evento inválido.");
        }

        if (string.IsNullOrWhiteSpace(e.EventoId) || e.EventoId.Length > 128 || !new[]
        {
            "APROBADO",
            "RECHAZADO",
            "ERROR"
        }.Contains(e.Resultado))
        {
            throw ApiContract.Invalid("Evento inválido.");
        }

        return await mutation.Run(h, e, async ct => ApiReply.Ok(await workflow.Apply(e, h.TraceIdentifier, ct)));
    }
}
