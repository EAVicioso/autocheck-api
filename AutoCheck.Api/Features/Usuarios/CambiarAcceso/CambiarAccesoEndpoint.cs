using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;

namespace AutoCheck.Api.Features.Usuarios.CambiarAcceso;

public static class CambiarAccesoEndpoint
{
    public static void MapCambiarAcceso(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/{id:guid}/acceso", async (Guid id, AccessRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Users);
            await db.LockAsync("user:" + id, ct);
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            AuthorizeChange(id, r, user, actor);
            await ValidateTarget(id, r, user, db, ct);

            db.Audit.Add(new() { ActorId = actor.Id, At = clock.GetUtcNow(), Operation = "CAMBIO_ACCESO", Entity = user.Id.ToString(), Correlation = h.TraceIdentifier, Outcome = ApiReply.Serialize(new { anterior = new { rol = user.Role, permisos = user.Permissions, centroId = user.CenterId, activo = user.Active }, nuevo = r }) });
            user.Role = r.Rol;
            user.Permissions = r.Permisos.Distinct().ToArray();
            user.CenterId = r.CentroId;
            user.Active = r.Activo;
            return ApiReply.Ok(Response(user));
        })).Interactive().Document<UserResponse>("CambiarAcceso", "CU-11 / PA-11a,b").Validated<AccessRequest>(v =>
        {
            v.RuleFor(x => x.Rol).Must(x => new[] { Roles.Citizen, Roles.Inspector, Roles.Admin }.Contains(x));
            v.RuleFor(x => x.Permisos).NotNull();
            v.RuleForEach(x => x.Permisos).Must(Permissions.All.Contains);
        });
    }

    private static void AuthorizeChange(Guid id, AccessRequest r, UserAccount user, AppActor actor)
    {
        if (id == actor.Id)
        {
            throw ApiContract.Conflict("CAMBIO_PROPIO_NO_PERMITIDO", "Otro administrador debe cambiar tu acceso.");
        }

        if (actor.CenterId is not null)
        {
            actor.RequireCenter(user.CenterId ?? Guid.Empty);
            if (r.CentroId != actor.CenterId || r.Rol == Roles.Admin || r.Permisos.Length != 0)
            {
                AppActor.Deny();
            }
        }

    }

    private static async Task ValidateTarget(Guid id, AccessRequest r, UserAccount user, AutoCheckDb db, CancellationToken ct)
    {
        if (r.CentroId is not null && !await db.Centers.AnyAsync(x => x.Id == r.CentroId && x.Active, ct))
        {
            throw ApiContract.Invalid("Centro inválido.");
        }

        if (r.Rol == Roles.Inspector && r.CentroId is null)
        {
            throw ApiContract.Invalid("Un inspector requiere un centro.");
        }

        if (r.Rol != Roles.Admin && r.Permisos.Length != 0)
        {
            throw ApiContract.Invalid("Los permisos administrativos requieren rol Administrador.");
        }

        if ((user.CenterId != r.CentroId || !r.Activo || r.Rol != user.Role) && await db.Appointments.AnyAsync(x => x.InspectorId == id && (x.State == "CONFIRMADA" || x.State == "EN_INSPECCION"), ct))
        {
            throw ApiContract.Conflict("ASIGNACIONES_PENDIENTES", "Resuelve las asignaciones del inspector antes de cambiar su acceso.");
        }

    }
}

public sealed record AccessRequest(string Rol, string[] Permisos, Guid? CentroId, bool Activo);
