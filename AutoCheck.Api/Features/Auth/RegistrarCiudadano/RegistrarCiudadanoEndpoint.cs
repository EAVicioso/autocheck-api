// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RegistrarCiudadano;

public static class RegistrarCiudadanoEndpoint
{
    public static void MapRegistrarCiudadano(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/registro", HandleAsync).AllowAnonymous().Document<RegistrationResponse>("RegistrarCiudadano", "CU-01 / PA-01 / RS-09", 201).Validated<RegisterRequest>();
    }

    private static async Task<Results<Created<RegistrationResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult, ForbidHttpResult>> HandleAsync(RegisterRequest r, HttpContext h, Handler handler, CancellationToken ct)
    {
        var result = await handler.Execute(r, h, ct);
        return TypedResults.Created("/api/usuarios/me", result);
    }
}
