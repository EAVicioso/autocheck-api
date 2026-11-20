namespace AutoCheck.Api.Infrastructure.Data;

// DEC-LIFECYCLE-01; shared integrity predicates, never an HTTP endpoint.
public static class Lifecycle
{
    public static void RequireVersion(long expected, long current, DateTimeOffset? archived)
    {
        if (expected < 1)
        {
            throw ApiContract.Invalid("La versión es obligatoria.");
        }
        if (archived is not null)
        {
            throw ApiContract.Conflict("RECURSO_ARCHIVADO", "El recurso está archivado.");
        }
        if (expected != current)
        {
            throw ApiContract.Conflict("VERSION_DESACTUALIZADA", "El registro cambió. Actualiza y reintenta.");
        }
    }
    public static async Task PreserveAdministrator(AutoCheckDb db, UserAccount user, bool remainsAdministrator, CancellationToken ct)
    {
        if (!remainsAdministrator && await IsLastAdministrator(db, user, ct))
        {
            throw ApiContract.Conflict("ULTIMO_ADMINISTRADOR", "Debe conservarse un administrador global activo.");
        }
    }
    public static async Task<bool> IsLastAdministrator(AutoCheckDb db, UserAccount user, CancellationToken ct) => user.Role == Roles.Admin && user.Active && user.CenterId == null && user.Permissions.Contains(Permissions.Users)
        && !await db.Users.AnyAsync(x => x.Id != user.Id && x.Role == Roles.Admin && x.Active && x.ArchivedAt == null && x.CenterId == null && x.Permissions.Contains(Permissions.Users), ct);

    public static async Task<bool> HasPending(AutoCheckDb db, Guid id, bool user, DateTimeOffset now, CancellationToken ct)
    {
        var appointment = await db.Appointments.AnyAsync(x => (user ? x.UserId == id || x.InspectorId == id : x.VehicleId == id) && (x.State == "CONFIRMADA" || x.State == "EN_INSPECCION"), ct);
        var inspection = await db.Inspections.AnyAsync(x => x.State != "CERRADA" && (user ? x.OwnerId == id || x.InspectorId == id : x.VehicleId == id), ct);
        var hold = await db.Payments.AnyAsync(x => x.State == "PENDIENTE" && x.Expires > now && (user ? x.UserId == id : x.VehicleId == id), ct);
        return appointment || inspection || hold;
    }
    public static async Task RequireNoPending(AutoCheckDb db, Guid id, bool user, DateTimeOffset now, CancellationToken ct)
    {
        if (await HasPending(db, id, user, now, ct))
        {
            throw ApiContract.Conflict("OPERACIONES_PENDIENTES", "Resuelve las operaciones pendientes antes de archivar.");
        }
    }
}
