// AutoCheck academic prototype, SRS 1.1; CU-03, PA-03e/04d.
namespace AutoCheck.Api.Features.Citas;

public sealed record CancelRequest(string Motivo);
public sealed record AssignInspectorRequest(Guid InspectorId);
public sealed record CancellationResponse(Guid CitaId, string Estado, Guid? ReversionId);
public sealed record AppointmentResponse(Guid Id, Guid VehiculoId, Guid CentroId, Guid RampaId, Guid? InspectorId, DateTimeOffset InicioUtc, string Estado, Guid? InspeccionAnteriorId);

public static class AppointmentEndpoints
{
    public static AppointmentResponse Response(Appointment a) => new(a.Id, a.VehicleId, a.CenterId, a.RampId, a.InspectorId, a.Starts, a.State, a.PreviousInspectionId);
    private static bool CanRead(AppActor actor, Appointment a) => a.UserId == actor.Id || (actor.Role == Roles.Inspector && actor.CenterId == a.CenterId && a.InspectorId == actor.Id) || actor.Manages(a.CenterId);
    public static void MapAppointments(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/citas").WithTags("Citas").RequireAuthorization();
        g.MapGet("", async (DateOnly? desde, DateOnly? hasta, string? estado, HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var (from, to) = ApiContract.UtcRange(desde, hasta); ApiContract.Filter(estado, "CONFIRMADA", "CANCELADA", "EN_INSPECCION", "COMPLETADA");
            return new {
            items = (await db.Appointments.AsNoTracking().Where(x => x.UserId == actor.Id || (actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) ||
                (actor.Role == Roles.Admin && (actor.CenterId == null || x.CenterId == actor.CenterId)))
                .Where(x => (from == null || x.Starts >= from) && (to == null || x.Starts < to) && (estado == null || x.State == estado))
                .OrderByDescending(x => x.Starts).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
            };
        }).Document<ItemsResponse<AppointmentResponse>>("ListarCitas", "CU-03 / 6.2.1.1");
        g.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var a = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!CanRead(actor, a)) AppActor.Deny(); return Response(a);
        }).Document<AppointmentResponse>("ObtenerCita", "CU-03");
        g.MapDelete("/retenciones/{id:guid}", async (Guid id, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, new { id }, async ct =>
        {
            var p = await db.Payments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (p.UserId != actor.Id) AppActor.Deny();
            await db.LockAsync("center:" + p.CenterId, ct); await db.Entry(p).ReloadAsync(ct);
            if (p.State == "APROBADO") throw ApiContract.Conflict("CITA_CONFIRMADA", "Cancela la cita confirmada mediante su operación de cancelación.");
            var b = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == p.RampId && x.Starts == p.Starts && x.AttemptId == id, ct);
            if (b?.State == "RETENIDO") db.Blocks.Remove(b);
            if (p.State == "PENDIENTE") p.State = "ABANDONADO";
            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = clock.GetUtcNow(), Payload = ApiReply.Serialize(new { centroId = p.CenterId }) });
            return ApiReply.Empty();
        })).Interactive().Document<object>("AbandonarRetencion", "RF-CIT-02 / CU-03", 204);
        g.MapPost("/{id:guid}/cancelacion", async (Guid id, CancelRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (a.UserId != actor.Id) AppActor.Deny();
            await db.LockAsync("center:" + a.CenterId, ct); await db.Entry(a).ReloadAsync(ct);
            var payment = await db.Payments.SingleAsync(x => x.Id == a.AttemptId, ct);
            if (a.State == "CANCELADA") return ApiReply.Ok(new { citaId = a.Id, estado = a.State, reversionId = payment.ReversalId });
            if (a.State != "CONFIRMADA" || a.Starts <= clock.GetUtcNow() || await db.Inspections.AnyAsync(x => x.AppointmentId == id, ct)) throw ApiContract.Conflict("CITA_NO_CANCELABLE", "La cita ya no puede cancelarse.");
            a.State = "CANCELADA"; payment.State = "REVERTIDO"; payment.ReversalId ??= Guid.NewGuid();
            var block = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == a.RampId && x.Starts == a.Starts && x.AttemptId == a.AttemptId, ct);
            if (block is not null) db.Blocks.Remove(block);
            db.Outbox.Add(new() { Kind = "CITA_CANCELADA", AggregateId = a.Id, Available = clock.GetUtcNow() });
            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = clock.GetUtcNow(), Payload = ApiReply.Serialize(new { centroId = a.CenterId }) });
            return ApiReply.Ok(new { citaId = a.Id, estado = a.State, reversionId = payment.ReversalId });
        }, true)).Interactive().Document<CancellationResponse>("CancelarCita", "RF-CIT-02 / PA-03e / PA-04d")
            .Validated<CancelRequest>(v => v.RuleFor(x => x.Motivo).NotEmpty().MaximumLength(300));
        g.MapPut("/{id:guid}/inspector", async (Guid id, AssignInspectorRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing(); actor.RequireCenter(a.CenterId);
            await db.LockAsync("center:" + a.CenterId, ct); await db.LockAsync("user:" + r.InspectorId, ct); await db.Entry(a).ReloadAsync(ct);
            if (a.State != "CONFIRMADA" || await db.Inspections.AnyAsync(x => x.AppointmentId == id, ct)) throw ApiContract.Conflict("CITA_INICIADA", "No se puede reasignar una inspección iniciada.");
            if (!await db.Users.AnyAsync(x => x.Id == r.InspectorId && x.Role == Roles.Inspector && x.Active && x.CenterId == a.CenterId, ct)) throw ApiContract.Invalid("Inspector no disponible en este centro.");
            a.InspectorId = r.InspectorId; return ApiReply.Ok(Response(a));
        })).Interactive().Document<AppointmentResponse>("AsignarInspector", "CU-05 / 6.2.1.1")
            .Validated<AssignInspectorRequest>(v => v.RuleFor(x => x.InspectorId).NotEmpty());
    }
}
