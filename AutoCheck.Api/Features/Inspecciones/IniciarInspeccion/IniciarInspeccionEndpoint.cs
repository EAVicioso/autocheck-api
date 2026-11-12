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

    private static async Task<ApiReply> HandleAsync(StartInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
    {
        actor.RequireRole(Roles.Inspector);
        var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == r.CitaId, ct) ?? throw ApiContract.Missing();
        await db.LockAsync("center:" + a.CenterId, ct);
        await db.LockAsync("rules", ct);
        await db.Entry(a).ReloadAsync(ct);
        if (a.InspectorId != actor.Id || actor.CenterId != a.CenterId)
        {
            AppActor.Deny();
        }

        if (a.State != "CONFIRMADA" || !await db.Payments.AnyAsync(x => x.Id == a.AttemptId && x.State == "APROBADO", ct))
        {
            throw ApiContract.Conflict("CITA_NO_CONFIRMADA", "La cita no está disponible para iniciar.");
        }

        var rule = await db.Rules.Where(x => x.Effective <= clock.GetUtcNow()).OrderByDescending(x => x.Effective).FirstAsync(ct);
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == a.VehicleId, ct);
        var center = await db.Centers.AsNoTracking().SingleAsync(x => x.Id == a.CenterId, ct);
        var i = new Inspection
        {
            AppointmentId = a.Id,
            OwnerId = a.UserId,
            VehicleId = a.VehicleId,
            CenterId = a.CenterId,
            RampId = a.RampId,
            InspectorId = actor.Id,
            PreviousId = a.PreviousInspectionId,
            Started = clock.GetUtcNow(),
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
}

public sealed record StartInspectionRequest(Guid CitaId);
