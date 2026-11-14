using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public static class RestablecerContrasenaEndpoint
{
    public static void MapRestablecerContrasena(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/restablecimiento", async (ResetRequest r, RestablecerContrasenaHandler handler, HttpContext http, CancellationToken ct) =>
        {
            await handler.Execute(r, http.TraceIdentifier, ct);
            return Results.NoContent();
        }).AllowAnonymous().Document<object>("RestablecerContrasena", "RS-09 / RS-11", 204).Validated<ResetRequest>(v =>
        {
            v.RuleFor(x => x.Token).NotEmpty().MaximumLength(8192);
            Password(v.RuleFor(x => x.NuevaContrasena));
        });
    }
}

public sealed record ResetRequest(string Token, string NuevaContrasena);
