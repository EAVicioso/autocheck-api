using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Infrastructure;

public sealed record ApiReply(int Status, string Json, string? Location = null) : IResult
{
    public static ApiReply Ok(object body) => new(200, Serialize(body));
    public static ApiReply Created(string location, object body) => new(201, Serialize(body), location);
    public static ApiReply Empty() => new(204, "");
    public static string Serialize(object body) => JsonSerializer.Serialize(body, JsonOptions);
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = Status;
        if (Location is not null)
        {
            httpContext.Response.Headers.Location = Location;
        }

        if (Status != 204)
        {
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            await httpContext.Response.WriteAsync(Json, httpContext.RequestAborted);
        }
    }
}
