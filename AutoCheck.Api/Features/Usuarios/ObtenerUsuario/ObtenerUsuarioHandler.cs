using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerUsuario;

public interface IData
{
    Task<UserResponse?> Read(Query query, CancellationToken ct);
}
public sealed class Handler(IData data, AppActor actor)
{
    public async Task<UserResponse> Execute(Query query, CancellationToken ct)
    {
        actor.RequirePermission(Permissions.Users);
        return await data.Read(query, ct) ?? throw ApiContract.Missing();
    }
}
