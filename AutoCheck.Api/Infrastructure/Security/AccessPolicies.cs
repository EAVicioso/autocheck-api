// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Infrastructure.Security;

public static class AccessPolicies
{
    public static IServiceCollection AddAccessPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy;
            options.AddPolicy("SesionActiva", p => p.RequireAuthenticatedUser().RequireAssertion(c => Actor(c.Resource)?.Id != Guid.Empty && Actor(c.Resource) is not null));
            options.AddPolicy("GestionUsuarios", p => p.RequireAuthenticatedUser().RequireAssertion(c => Has(Actor(c.Resource), Permissions.Users)));
            options.AddPolicy("AuditoriaGlobal", p => p.RequireAuthenticatedUser().RequireAssertion(c => Has(Actor(c.Resource), Permissions.Audit) && Actor(c.Resource)?.CenterId is null));
            options.AddPolicy("Ciudadano", p => p.RequireAuthenticatedUser().RequireAssertion(c => Actor(c.Resource)?.Role == Roles.Citizen));
        });
        return services;
    }
    private static AppActor? Actor(object? resource) => (resource as HttpContext)?.RequestServices.GetRequiredService<AppActor>();
    private static bool Has(AppActor? actor, string permission) => actor?.Role == Roles.Admin && actor.Granted.Contains(permission);
}
public sealed record LogoutOperation(bool AllowRevoked);
