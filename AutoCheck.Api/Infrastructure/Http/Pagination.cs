using Microsoft.AspNetCore.WebUtilities;
namespace AutoCheck.Api.Infrastructure;

public sealed record PageResponse<T>(ImmutableArray<T> Items, int? Pagina, string? SiguienteCursor);
public sealed record SeekCursor(int Version, string Orden, string Direccion, DateTimeOffset Fecha, Guid Id);
public sealed record PageSettings(int Page, int Size, string Order, bool Descending, SeekCursor? Cursor)
{
    private static readonly JsonSerializerOptions CursorOptions = new() { MaxDepth = 8 };
    public static PageSettings Create(int? pagina, int? tamano, string? orden, string? direccion, string? cursor, string defaultOrder, bool defaultDescending, params string[] allowed)
    {
        var page = pagina ?? 1;
        var size = tamano ?? 20;
        if (page is < 1 or > 500000 || size is < 1 or > 20)
        {
            throw ApiContract.Invalid("Página o tamaño inválidos.");
        }
        var order = orden ?? defaultOrder;
        var direction = direccion ?? (defaultDescending ? "desc" : "asc");
        ApiContract.Filter(order, allowed);
        ApiContract.Filter(direction, "asc", "desc");
        var seek = cursor is null ? null : Decode(cursor, order, direction);
        if (seek is not null && page != 1)
        {
            throw ApiContract.Invalid("No combines cursor con pagina mayor a uno.");
        }
        return new(page, size, order, direction == "desc", seek);
    }
    private static SeekCursor Decode(string value, string order, string direction)
    {
        try
        {
            if (value.Length is 0 or > 1024)
            {
                throw new FormatException();
            }
            var decoded = JsonSerializer.Deserialize<SeekCursor>(WebEncoders.Base64UrlDecode(value), CursorOptions);
            if (decoded is null || decoded.Version != 1 || decoded.Orden != order || decoded.Direccion != direction || decoded.Id == Guid.Empty || decoded.Fecha.Offset != TimeSpan.Zero)
            {
                throw new FormatException();
            }
            return decoded;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException)
        {
            throw new ApiException(400, "CURSOR_INVALIDO", "Cursor inválido o incompatible con el orden.");
        }
    }
    public string Encode(DateTimeOffset at, Guid id) => WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new SeekCursor(1, Order, Descending ? "desc" : "asc", at.ToUniversalTime(), id)));
}
