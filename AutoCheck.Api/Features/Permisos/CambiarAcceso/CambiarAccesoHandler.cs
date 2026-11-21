using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Permisos.CambiarAcceso;

public interface IData
{
    Task<(int Status, UserResponse? Body, string? Location)> Execute(Guid id, AccessRequest r, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken);
}
public sealed class Handler(IData data, AppActor actor)
{
    public Task<(int Status, UserResponse? Body, string? Location)> Execute(Guid id, AccessRequest r, HttpContext h, CancellationToken cancellationToken)
    {
        actor.RequirePermission(Permissions.Users);
        if (id == actor.Id)
        {
            throw ApiContract.Conflict(ApiErrorCode.CambioPropioNoPermitido, "Otro administrador debe cambiar tu acceso.");
        }
        return data.Execute(id, r, h, state => Validate(state, r), cancellationToken);
    }
    private void Validate(Snapshot state, AccessRequest request)
    {
        var user = state.User;
        AuthorizeScope(user, request);
        Lifecycle.RequireVersion(request.Version ?? user.Version, user.Version, user.ArchivadoUtc);
        ValidateAssignment(user, request, state);
        var remains = request.Rol == Roles.Admin && request.Activo && request.CentroId == null && request.Permisos.Contains(Permissions.Users);
        if (state.LastAdministrator && !remains)
        {
            throw ApiContract.Conflict(ApiErrorCode.UltimoAdministrador, "Debe conservarse un administrador global activo.");
        }
    }
    private void AuthorizeScope(UserResponse user, AccessRequest request)
    {
        if (actor.CenterId is null)
        {
            return;
        }
        actor.RequireCenter(user.CentroId ?? Guid.Empty);
        if (request.CentroId != actor.CenterId || request.Rol == Roles.Admin || request.Permisos.Length != 0)
        {
            AppActor.Deny();
        }
    }
    private static void ValidateAssignment(UserResponse user, AccessRequest request, Snapshot state)
    {
        if (!state.CenterValid || (request.Rol == Roles.Inspector && request.CentroId == null))
        {
            throw ApiContract.Invalid("Centro inválido para el rol.");
        }
        if (request.Rol != Roles.Admin && request.Permisos.Length != 0)
        {
            throw ApiContract.Invalid("Los permisos administrativos requieren rol Administrador.");
        }
        if ((user.CentroId != request.CentroId || !request.Activo || request.Rol != user.Rol) && state.PendingAssignments)
        {
            throw ApiContract.Conflict(ApiErrorCode.AsignacionesPendientes, "Resuelve las asignaciones del inspector antes de cambiar su acceso.");
        }
    }
}
