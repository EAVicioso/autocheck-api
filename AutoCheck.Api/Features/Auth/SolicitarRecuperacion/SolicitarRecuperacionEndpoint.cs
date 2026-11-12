using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

public static class SolicitarRecuperacionEndpoint
{
    public static void MapSolicitarRecuperacion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/recuperacion", async (RecoveryRequest r, IIdentityProvider provider, CancellationToken ct) =>
        {
            await provider.Recover(ApiContract.NormalizeEmail(r.Correo), ct);
            return Results.Accepted(value: new { mensaje = "Si la cuenta existe, recibirás instrucciones de recuperación." });
        }).AllowAnonymous().Document<object>("SolicitarRecuperacion", "IS-04 / RS-11", 202).Validated<RecoveryRequest>(v => v.RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254));
    }
}

public sealed record RecoveryRequest(string Correo);
