using AutoCheck.Api.Features.Citas.Common;
using static AutoCheck.Api.Features.Citas.Common.AppointmentSupport;

namespace AutoCheck.Api.Features.Citas.AsignarInspector;

public static class AsignarInspectorEndpoint
{
    public static void MapAsignarInspector(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/inspector", async (Guid id, AssignInspectorRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            actor.RequireCenter(a.CenterId);
            await db.LockAsync("center:" + a.CenterId, ct);
            await db.LockAsync("user:" + r.InspectorId, ct);
            await db.Entry(a).ReloadAsync(ct);
            if (a.State != "CONFIRMADA" || await db.Inspections.AnyAsync(x => x.AppointmentId == id, ct))
            {
                throw ApiContract.Conflict("CITA_INICIADA", "No se puede reasignar una inspección iniciada.");
            }

            if (!await db.Users.AnyAsync(x => x.Id == r.InspectorId && x.Role == Roles.Inspector && x.Active && x.CenterId == a.CenterId, ct))
            {
                throw ApiContract.Invalid("Inspector no disponible en este centro.");
            }

            a.InspectorId = r.InspectorId;
            return ApiReply.Ok(Response(a));
        })).Interactive().Document<AppointmentResponse>("AsignarInspector", "CU-05 / 6.2.1.1").Validated<AssignInspectorRequest>(v => v.RuleFor(x => x.InspectorId).NotEmpty());
    }
}

public sealed record AssignInspectorRequest(Guid InspectorId);
