// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1; Section C / PA-05/08/09/10. Version 1.0; history: Git.
using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.IniciarInspeccion;

public static class IniciarInspeccionEndpoint
{
    public static void MapIniciarInspeccion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("", HandleAsync).Interactive().Document<InspectionResponse>("IniciarInspeccion", "RF-INS-01 / PA-05b", 201).Validated<StartInspectionRequest>(v => v.RuleFor(x => x.CitaId).NotEmpty());
    }

    private static async Task<ApiReply> HandleAsync(StartInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock)
    {
        actor.RequireRole(Roles.Inspector);
        var reply = await mutation.Run(h, r, async ct =>
        {
            var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == r.CitaId, ct) ?? throw ApiContract.Missing();
            await db.LockAsync("center:" + a.CenterId, ct);
            await db.LockAsync("user:" + actor.Id, ct);
            await db.LockAsync("vehicle:" + a.VehicleId, ct);
            await RequireActiveResources(db, actor.Id, a.VehicleId, ct);
            await db.LockAsync("rules", ct);
            await actor.RevalidateAuthority(db, clock, ct);
            actor.RequireRole(Roles.Inspector);
            await db.Entry(a).ReloadAsync(ct);
            if (a.InspectorId != actor.Id || actor.CenterId != a.CenterId)
            {
                AppActor.Deny();
            }

            if (a.State != "CONFIRMADA" || !await db.Payments.AnyAsync(x => x.Id == a.AttemptId && x.State == "APROBADO", ct))
            {
                throw ApiContract.Conflict("CITA_NO_CONFIRMADA", "La cita no está disponible para iniciar.");
            }

            var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == a.VehicleId, ct);
            var center = await db.Centers.AsNoTracking().SingleAsync(x => x.Id == a.CenterId, ct);
            var started = clock.GetUtcNow();
            var rule = await db.Rules.AsNoTracking().Where(x => x.Effective <= started)
                .OrderByDescending(x => x.Effective).FirstAsync(ct);
            var i = new Inspection
            {
                AppointmentId = a.Id,
                OwnerId = a.UserId,
                VehicleId = a.VehicleId,
                CenterId = a.CenterId,
                RampId = a.RampId,
                InspectorId = actor.Id,
                PreviousId = a.PreviousInspectionId,
                Started = started,
                RuleVersion = rule.Version,
                MaxMinor = rule.MaxMinor,
                MongoId = ObjectId.GenerateNewId().ToString(),
                VehicleSnapshot = ApiReply.Serialize(VehicleSupport.Response(vehicle)),
                CenterSnapshot = ApiReply.Serialize(new { nombre = center.Name, direccion = center.Address })
            };
            db.Inspections.Add(i);
            a.State = "EN_INSPECCION";
            return ApiReply.Created($"/api/inspecciones/{i.Id}", Response(i));
        }, true);
        // Replays must also respect the current appointment assignment and center.
        var assignment = await db.Appointments.AsNoTracking().Where(x => x.Id == r.CitaId)
            .Select(x => new { x.InspectorId, x.CenterId }).SingleOrDefaultAsync(h.RequestAborted) ?? throw ApiContract.Missing();
        if (assignment.InspectorId != actor.Id || assignment.CenterId != actor.CenterId)
        {
            AppActor.Deny();
        }
        return reply;
    }
    private static async Task RequireActiveResources(AutoCheckDb db, Guid inspectorId, Guid vehicleId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == inspectorId && x.Active && x.ArchivedAt == null, ct) ||
            !await db.Vehicles.AnyAsync(x => x.Id == vehicleId && x.ArchivedAt == null, ct))
        {
            AppActor.Deny();
        }
    }
}

public sealed record StartInspectionRequest(Guid CitaId);
