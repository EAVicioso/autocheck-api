using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public sealed class Data(AutoCheckDb db, AppActor actor, Mutation mutation) : IData
{
    public Task<(int Status, UserResponse? Body, string? Location)> Execute(ProfileRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<UserResponse>(h, r, async ct =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == actor.Id, ct);
            validate(new Snapshot(u.Version, u.ArchivedAt));
            u.Version++;
            u.Name = r.Nombre.Trim();
            u.Phone = r.Celular.Trim();
            return (200, Response(u), (string?)null);
        });
    }

}
