// AutoCheck academic prototype, SRS 1.1; CU-11, PA-11.
namespace AutoCheck.Api.Features.Usuarios;

public sealed record ProfileRequest(string Nombre, string Celular);
public sealed record AccessRequest(string Rol, string[] Permisos, Guid? CentroId, bool Activo);
public sealed record UserResponse(Guid Id, string Correo, string Nombre, string Celular, string Rol, string[] Permisos, Guid? CentroId, bool Activo);
public sealed record UserListResponse(IEnumerable<UserResponse> Items, int Pagina);

public static class UserEndpoints
{
    public static UserResponse Response(UserAccount u) => new(u.Id, u.Email, u.Name, u.Phone, u.Role, u.Permissions, u.CenterId, u.Active);
    public static void MapUsers(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/usuarios").WithTags("Usuarios").RequireAuthorization();
        g.MapGet("/me", async (AutoCheckDb db, AppActor actor, CancellationToken ct) => Response(await db.Users.AsNoTracking().SingleAsync(x => x.Id == actor.Id, ct)))
            .Document<UserResponse>("ObtenerPerfil", "6.2.1.1 / CU-11");
        g.MapPatch("/me", async (ProfileRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            var u = await db.Users.SingleAsync(x => x.Id == actor.Id, ct);
            u.Name = r.Nombre.Trim(); u.Phone = r.Celular.Trim();
            return ApiReply.Ok(Response(u));
        })).Interactive().Document<UserResponse>("ActualizarPerfil", "6.2.1.1")
            .Validated<ProfileRequest>(v => { v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120); v.RuleFor(x => x.Celular).NotNull().MaximumLength(30); });
        g.MapGet("", async (string? rol, bool? activo, HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            actor.RequirePermission(Permissions.Users);
            ApiContract.Filter(rol, Roles.Admin, Roles.Inspector, Roles.Citizen);
            var q = db.Users.AsNoTracking().Where(x => (actor.CenterId == null || x.CenterId == actor.CenterId) && (rol == null || x.Role == rol) && (activo == null || x.Active == activo));
            var page = ApiContract.Page(h); var size = ApiContract.Size(h);
            return new { items = (await q.OrderBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct)).Select(Response), pagina = page };
        }).Document<UserListResponse>("ListarUsuarios", "CU-11 / PA-11");
        g.MapPatch("/{id:guid}/acceso", async (Guid id, AccessRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Users);
            await db.LockAsync("user:" + id, ct);
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (id == actor.Id) throw ApiContract.Conflict("CAMBIO_PROPIO_NO_PERMITIDO", "Otro administrador debe cambiar tu acceso.");
            if (actor.CenterId is not null)
            {
                actor.RequireCenter(user.CenterId ?? Guid.Empty);
                if (r.CentroId != actor.CenterId || r.Rol == Roles.Admin || r.Permisos.Length != 0) AppActor.Deny();
            }
            if (r.CentroId is not null && !await db.Centers.AnyAsync(x => x.Id == r.CentroId && x.Active, ct)) throw ApiContract.Invalid("Centro inválido.");
            if (r.Rol == Roles.Inspector && r.CentroId is null) throw ApiContract.Invalid("Un inspector requiere un centro.");
            if (r.Rol != Roles.Admin && r.Permisos.Length != 0) throw ApiContract.Invalid("Los permisos administrativos requieren rol Administrador.");
            if ((user.CenterId != r.CentroId || !r.Activo || r.Rol != user.Role) &&
                await db.Appointments.AnyAsync(x => x.InspectorId == id && (x.State == "CONFIRMADA" || x.State == "EN_INSPECCION"), ct))
                throw ApiContract.Conflict("ASIGNACIONES_PENDIENTES", "Resuelve las asignaciones del inspector antes de cambiar su acceso.");
            db.Audit.Add(new() { ActorId = actor.Id, At = clock.GetUtcNow(), Operation = "CAMBIO_ACCESO", Entity = user.Id.ToString(), Correlation = h.TraceIdentifier,
                Outcome = ApiReply.Serialize(new { anterior = new { rol = user.Role, permisos = user.Permissions, centroId = user.CenterId, activo = user.Active }, nuevo = r }) });
            user.Role = r.Rol; user.Permissions = r.Permisos.Distinct().ToArray(); user.CenterId = r.CentroId; user.Active = r.Activo;
            return ApiReply.Ok(Response(user));
        })).Interactive().Document<UserResponse>("CambiarAcceso", "CU-11 / PA-11a,b")
            .Validated<AccessRequest>(v => { v.RuleFor(x => x.Rol).Must(x => new[] { Roles.Citizen, Roles.Inspector, Roles.Admin }.Contains(x)); v.RuleFor(x => x.Permisos).NotNull(); v.RuleForEach(x => x.Permisos).Must(Permissions.All.Contains); });
    }
}
