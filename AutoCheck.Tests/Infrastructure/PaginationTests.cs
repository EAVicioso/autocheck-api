using AutoCheck.Api.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
namespace AutoCheck.Tests.Infrastructure;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 21)]
    public void InvalidBoundsAreRejected(int page, int size) => Assert.Throws<ApiException>(() => PageSettings.Create(page, size, null, null, null, "fechaUtc", true, "fechaUtc"));
    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    public void InvalidCursorsAreTyped(string cursor)
    {
        var error = Assert.Throws<ApiException>(() => PageSettings.Create(1, 20, null, null, cursor, "fechaUtc", true, "fechaUtc"));
        Assert.Equal("CURSOR_INVALIDO", error.Code);
    }
    [Fact]
    public void CursorRoundtripBindsOrderAndDirection()
    {
        var page = PageSettings.Create(1, 2, null, null, null, "fechaUtc", true, "fechaUtc");
        var id = Guid.NewGuid();
        var value = page.Encode(DateTimeOffset.UnixEpoch, id);
        var next = PageSettings.Create(1, 2, null, null, value, "fechaUtc", true, "fechaUtc");
        Assert.Equal(id, next.Cursor?.Id);
        Assert.Throws<ApiException>(() => PageSettings.Create(1, 2, null, "asc", value, "fechaUtc", true, "fechaUtc"));
        Assert.Throws<ApiException>(() => PageSettings.Create(2, 2, null, null, value, "fechaUtc", true, "fechaUtc"));
    }
    [Fact]
    public void CursorWithExcessiveNestingIsRejected()
    {
        var input = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("[[[[[[[[[[]]]]]]]]]]"));
        Assert.Throws<ApiException>(() => PageSettings.Create(1, 2, null, null, input, "fechaUtc", true, "fechaUtc"));
    }
}
