using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public static class RenovarSesionEndpoint
{
    public static void MapRenovarSesion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/refresh", HandleAsync).AllowAnonymous().Document<SessionResponse>("RenovarSesion", "CU-02 / RS-10").Validated<RefreshRequest>();
    }

    private static async Task<Results<Ok<SessionResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(RefreshRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var result = await handler.Execute(r, h, ct);
        return TypedResults.Ok(result);
    }
}
