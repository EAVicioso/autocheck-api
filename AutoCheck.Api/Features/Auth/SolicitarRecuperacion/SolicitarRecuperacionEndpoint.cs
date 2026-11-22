// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

public static class SolicitarRecuperacionEndpoint
{
    public static void MapSolicitarRecuperacion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/recuperacion", HandleAsync).AllowAnonymous().Document<RecoveryResponse>("SolicitarRecuperacion", "IS-04 / RS-11", 202).Validated<RecoveryRequest>();
    }
    private static async Task<Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Accepted<RecoveryResponse>, Microsoft.AspNetCore.Http.HttpResults.ValidationProblem, Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>> HandleAsync(RecoveryRequest request, Handler handler, CancellationToken ct) => TypedResults.Accepted(uri: (string?)null, value: await handler.Execute(request, ct));
}
