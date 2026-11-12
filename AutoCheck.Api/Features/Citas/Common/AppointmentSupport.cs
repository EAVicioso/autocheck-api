using AutoCheck.Api.Features.Citas;
using AutoCheck.Api.Features.Citas.Common;

namespace AutoCheck.Api.Features.Citas.Common;

public static class AppointmentSupport
{
    public static AppointmentResponse Response(Appointment a) => new(a.Id, a.VehicleId, a.CenterId, a.RampId, a.InspectorId, a.Starts, a.State, a.PreviousInspectionId);
    public static bool CanRead(AppActor actor, Appointment a) => a.UserId == actor.Id || (actor.Role == Roles.Inspector && actor.CenterId == a.CenterId && a.InspectorId == actor.Id) || actor.Manages(a.CenterId);
}
