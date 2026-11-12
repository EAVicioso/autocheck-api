using AutoCheck.Api.Features.Centros;
using AutoCheck.Api.Features.Centros.Common;

namespace AutoCheck.Api.Features.Centros.Common;

public static class CenterSupport
{
    public static CenterResponse Response(Center c) => new(c.Id, c.Name, c.Address, c.Active);
    public static async Task<bool> HasCommitments(AutoCheckDb db, Guid center, Guid? ramp, DateTimeOffset now, CancellationToken ct) => await db.Appointments.AnyAsync(x => x.CenterId == center && (ramp == null || x.RampId == ramp) && (x.State == "EN_INSPECCION" || (x.State == "CONFIRMADA" && x.Starts > now)), ct) || await db.Blocks.AnyAsync(x => x.State == "RETENIDO" && x.Expires > now && (ramp == null || x.RampId == ramp) && db.Ramps.Any(r => r.Id == x.RampId && r.CenterId == center), ct);
    public static void CenterValidation(RouteHandlerBuilder b) => b.Interactive().Validated<CenterRequest>(v =>
    {
        v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
        v.RuleFor(x => x.Direccion).NotEmpty().MaximumLength(300);
    });
    public static void RampValidation(RouteHandlerBuilder b) => b.Interactive().Validated<RampRequest>(v => v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80));
}
