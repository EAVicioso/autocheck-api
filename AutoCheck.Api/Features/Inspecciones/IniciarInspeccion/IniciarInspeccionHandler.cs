// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.
using AutoCheck.Api.Features.Vehiculos;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones;

public sealed record StartInspectionRequest(Guid CitaId);

public static partial class InspectionEndpoints
{
    private static async Task<ApiReply> Start(StartInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
    {
        actor.RequireRole(Roles.Inspector);
        var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == r.CitaId, ct) ?? throw ApiContract.Missing();
        await db.LockAsync("center:" + a.CenterId, ct); await db.LockAsync("rules", ct); await db.Entry(a).ReloadAsync(ct);
        if (a.InspectorId != actor.Id || actor.CenterId != a.CenterId) AppActor.Deny();
        if (a.State != "CONFIRMADA" || !await db.Payments.AnyAsync(x => x.Id == a.AttemptId && x.State == "APROBADO", ct)) throw ApiContract.Conflict("CITA_NO_CONFIRMADA", "La cita no está disponible para iniciar.");
        var rule = await db.Rules.Where(x => x.Effective <= clock.GetUtcNow()).OrderByDescending(x => x.Effective).FirstAsync(ct);
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == a.VehicleId, ct);
        var center = await db.Centers.AsNoTracking().SingleAsync(x => x.Id == a.CenterId, ct);
        var i = new Inspection { AppointmentId = a.Id, OwnerId = a.UserId, VehicleId = a.VehicleId, CenterId = a.CenterId, RampId = a.RampId, InspectorId = actor.Id, PreviousId = a.PreviousInspectionId,
            Started = clock.GetUtcNow(), RuleVersion = rule.Version, MaxMinor = rule.MaxMinor, MongoId = ObjectId.GenerateNewId().ToString(), VehicleSnapshot = ApiReply.Serialize(VehicleEndpoints.Response(vehicle)),
            CenterSnapshot = ApiReply.Serialize(new { nombre = center.Name, direccion = center.Address }) };
        db.Inspections.Add(i); a.State = "EN_INSPECCION";
        return ApiReply.Created($"/api/inspecciones/{i.Id}", Response(i));
    }, true);
}
