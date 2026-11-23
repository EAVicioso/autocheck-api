// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Permisos.CambiarAcceso;

public sealed class Data(AutoCheckDb db, Mutation mutation, AppActor actor, TimeProvider clock) : IData
{
    public Task<(int Status, UserResponse? Body, string? Location)> Execute(Guid id, AccessRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<UserResponse>(h, r, async ct =>
        {
            await db.LockAsync("admin-continuity", ct);
            await db.LockAsync("user:" + id, ct);
            await actor.RevalidateUserManagement(db, clock, ct);
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            var centerValid = r.CentroId == null || await db.Centers.AnyAsync(x => x.Id == r.CentroId && x.Active, ct);
            var pending = await db.Appointments.AnyAsync(x => x.InspectorId == id && (x.State == "CONFIRMADA" || x.State == "EN_INSPECCION"), ct);
            validate(new Snapshot(Response(user), centerValid, pending, await Lifecycle.IsLastAdministrator(db, user, ct)));

            user.Version++;
            user.Role = r.Rol;
            user.Permissions = r.Permisos.Distinct().ToArray();
            user.CenterId = r.CentroId;
            user.Active = r.Activo;
            return (200, Response(user), (string?)null);
        });
    }
}
