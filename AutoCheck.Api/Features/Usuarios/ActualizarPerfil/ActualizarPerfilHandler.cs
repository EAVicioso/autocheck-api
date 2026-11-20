using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public interface IData
{
    Task<(int Status, UserResponse? Body, string? Location)> Execute(ProfileRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data)
{
    public Task<(int Status, UserResponse? Body, string? Location)> Execute(ProfileRequest r, HttpContext h, CancellationToken cancellationToken)
    {
        return data.Execute(r with { Nombre = r.Nombre.Trim(), Celular = r.Celular.Trim() }, h, state => Lifecycle.RequireVersion(r.Version, state.Version, state.ArchivedAt), cancellationToken);
    }
}
