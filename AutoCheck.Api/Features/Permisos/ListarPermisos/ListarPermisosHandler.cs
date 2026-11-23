// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using System.Collections.Immutable;
namespace AutoCheck.Api.Features.Permisos.ListarPermisos;

public sealed class Handler(AppActor actor)
{
    private static readonly ItemsResponse<Response> Catalog = new(Permissions.All.Order(StringComparer.Ordinal).Select(x => new Response(x)).ToImmutableArray());
    public ItemsResponse<Response> Execute()
    {
        actor.RequirePermission(Permissions.Users);
        return Catalog;
    }
}
