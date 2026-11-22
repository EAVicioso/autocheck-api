// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.IniciarSesion;

public static class IniciarSesionEndpoint
{
    public static void MapIniciarSesion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/login", HandleAsync).AllowAnonymous().Document<SessionResponse>("IniciarSesion", "CU-02 / PA-02 / RS-11").Validated<LoginRequest>();
    }

    private static async Task<Results<Ok<SessionResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(LoginRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var result = await handler.Execute(r, h, ct);
        return TypedResults.Ok(result);
    }
}
