namespace AutoCheck.Api.Features.Auditorias.Common;

public sealed record AuditResponse(Guid Id, Guid? ActorId, DateTimeOffset FechaUtc, string Operacion, string Entidad, string Resultado, string Correlacion, string? Ip, JsonElement? Anterior, JsonElement? Nuevo);
public static class AuditJson
{
    public static JsonElement? Read(string? value)
    {
        if (value is null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
