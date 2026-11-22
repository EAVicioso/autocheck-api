// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Permisos.ObtenerAcceso;

public interface IData
{
    Task<AccessResponse?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public async Task<AccessResponse> Execute(Query query, CancellationToken ct)
    {
        actor.RequirePermission(Permissions.Users);
        return await data.Read(query, ct) ?? throw ApiContract.Missing();
    }
}
