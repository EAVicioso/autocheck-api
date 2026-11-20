using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ObtenerPerfil;

public interface IData
{
    Task<UserResponse?> Read(CancellationToken ct);
}
public sealed class Handler(IData data)
{
    public async Task<UserResponse> Execute(CancellationToken ct)
    {

        return await data.Read(ct) ?? throw ApiContract.Missing();
    }
}
