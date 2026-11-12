using AutoCheck.Api.Features.Inspecciones;
using AutoCheck.Api.Features.Inspecciones.Common;

namespace AutoCheck.Api.Features.Inspecciones.Common;

public static class InspectionSupport
{
    public static InspectionResponse Response(Inspection i) => new(i.Id, i.VehicleId, i.CenterId, i.RampId, i.InspectorId, i.PreviousId, i.Started, i.Closed, i.State, i.Result, i.RuleVersion, i.MaxMinor, i.Minor, i.Medium, i.Serious, i.Revision, i.Defects is null ? null : JsonSerializer.Deserialize<DefectEntry[]>(i.Defects, ApiReply.JsonOptions));
    public static bool CanRead(AppActor a, Inspection i) => a.Id == i.OwnerId || (a.Role == Roles.Inspector && a.CenterId == i.CenterId && a.Id == i.InspectorId) || a.Manages(i.CenterId);
    public static async Task<Inspection> Editable(Guid id, int version, AutoCheckDb db, AppActor actor, CancellationToken ct)
    {
        await db.LockAsync("inspection:" + id, ct);
        var i = await db.Inspections.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
        if (actor.Role != Roles.Inspector || i.InspectorId != actor.Id || actor.CenterId != i.CenterId)
        {
            AppActor.Deny();
        }

        if (i.State != "ABIERTA")
        {
            throw ApiContract.Conflict("INSPECCION_CERRADA", "La inspección cerrada es inmutable.");
        }

        if (i.Revision != version)
        {
            throw ApiContract.Conflict("VERSION_DESACTUALIZADA", "Actualiza la inspección antes de guardar.");
        }

        return i;
    }
}
