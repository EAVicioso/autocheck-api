// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Net;
using System.Text.Json.Serialization;
namespace AutoCheck.Api.Infrastructure;

public sealed class IpAddressJsonConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!IPAddress.TryParse(reader.GetString(), out var address))
        {
            throw new JsonException("Invalid IP address.");
        }
        return address;
    }
    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
