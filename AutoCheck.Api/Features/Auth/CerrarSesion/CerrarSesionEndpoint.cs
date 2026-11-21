using Microsoft.AspNetCore.Http.HttpResults;
using System.Net.Http.Headers;
namespace AutoCheck.Api.Features.Auth.CerrarSesion;

public static class CerrarSesionEndpoint
{
    public static void MapCerrarSesion(this IEndpointRouteBuilder endpoints) => endpoints.MapPost("/logout", HandleAsync).RequireAuthorization().WithMetadata(new LogoutOperation(true)).Document<object>("CerrarSesion", "CU-02 / RS-10", 204);
    private static async Task<NoContent> HandleAsync(HttpContext http, Handler handler, CancellationToken ct)
    {
        var header = AuthenticationHeaderValue.Parse(http.Request.Headers.Authorization.ToString());
        await handler.Execute(new Command(header.Parameter ?? ""), ct);
        return TypedResults.NoContent();
    }
}
