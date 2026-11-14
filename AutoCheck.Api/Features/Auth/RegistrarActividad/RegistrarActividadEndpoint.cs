using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RegistrarActividad;

public static class RegistrarActividadEndpoint
{
    public static void MapRegistrarActividad(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/actividad", () => Results.NoContent()).RequireAuthorization().Interactive().Document<object>("RegistrarActividad", "RS-10: acción explícita del usuario, nunca polling", 204);
    }
}
