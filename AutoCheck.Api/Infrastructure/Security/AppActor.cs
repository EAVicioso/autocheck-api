// AutoCheck academic prototype, SRS 1.1; 6.2.1.1, RS-10.
using System.Security.Claims;

namespace AutoCheck.Api.Infrastructure.Security;

public static class Roles
{
    public const string Citizen = "Ciudadano";
    public const string Inspector = "Inspector";
    public const string Admin = "Administrador";
    public static string FromLegacy(string role) => role switch
    {
        "Propietario" => Citizen,
        "AdminTaller" or "AdminINTRANT" => Admin,
        Citizen or Inspector or Admin => role,
        _ => throw ApiContract.Invalid("Rol desconocido.")
    };
}

public static class Permissions
{
    public const string Users = "usuarios.gestionar";
    public const string Centers = "centros.gestionar";
    public const string Rules = "reglas.gestionar";
    public const string Audit = "auditoria.consultar";
    public static readonly string[] All = [Users, Centers, Rules, Audit];
}

public sealed class AppActor
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "";
    public Guid? CenterId { get; set; }
    public string[] Granted { get; set; } = [];

    public void RequireRole(string role)
    {
        if (Role != role)
        {
            Deny();
        }
    }

    public void RequirePermission(string permission)
    {
        if (Role != Roles.Admin || !Granted.Contains(permission))
        {
            Deny();
        }
    }

    public bool Manages(Guid center) => Role == Roles.Admin && (CenterId is null || CenterId == center);
    public void RequireCenter(Guid center)
    {
        if (!Manages(center))
        {
            Deny();
        }
    }

    public static void Deny() => throw new ApiException(403, "ACCESO_DENEGADO", "No tienes permiso para esta operación.");
}

public sealed class SessionGuard(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AutoCheckDb db, AppActor actor, TimeProvider clock)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is null)
        {
            if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var subject) || !Guid.TryParse(context.User.FindFirstValue("session_id"), out var sessionId))
            {
                throw new ApiException(401, "SESION_INVALIDA", "Sesión inválida.");
            }

            var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, context.RequestAborted);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.AuthId == subject, context.RequestAborted);
            var now = clock.GetUtcNow();
            if (session is null || user is null || session.UserId != user.Id || session.Revoked || !user.Active || user.PasswordResetPending || session.SecurityVersion != user.SessionVersion || now - session.LastActivity >= TimeSpan.FromHours(1))
            {
                throw new ApiException(401, "SESION_EXPIRADA", "La sesión expiró o fue revocada.");
            }

            actor.Id = user.Id;
            actor.Role = user.Role;
            actor.Granted = user.Permissions;
            actor.CenterId = user.CenterId;
            actor.SessionId = sessionId;
            // Discard provider-supplied business roles. The database is the authority.
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", subject.ToString()), new("session_id", sessionId.ToString()), new(ClaimTypes.Role, user.Role)], "Application"));
            await next(context);
            // Explicit foreground actions only; GET polling and token refresh cannot extend inactivity.
            if (context.Response.StatusCode < 400 && context.GetEndpoint()?.Metadata.GetMetadata<InteractiveOperation>()?.RefreshActivity == true)
            {
                await db.Sessions.Where(x => x.Id == sessionId && !x.Revoked && x.LastActivity < now).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivity, now), context.RequestAborted);
            }

            return;
        }

        await next(context);
    }
}

public sealed record InteractiveOperation(bool RefreshActivity);
public static class InteractiveRoutes
{
    public static RouteHandlerBuilder Interactive(this RouteHandlerBuilder route) => route.WithMetadata(new InteractiveOperation(true));
}
