// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Collections.Immutable;
namespace AutoCheck.Api.Features.Permisos.ListarRoles;

public sealed class Handler(AppActor actor)
{
    private static readonly ItemsResponse<Response> Catalog = new(new[] { Roles.Admin, Roles.Citizen, Roles.Inspector }.Order(StringComparer.Ordinal).Select(x => new Response(x)).ToImmutableArray());
    public ItemsResponse<Response> Execute()
    {
        actor.RequirePermission(Permissions.Users);
        return Catalog;
    }
}
