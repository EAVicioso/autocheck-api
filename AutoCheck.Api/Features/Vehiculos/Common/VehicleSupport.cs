using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;

namespace AutoCheck.Api.Features.Vehiculos.Common;

public static class VehicleSupport
{
    public static VehicleResponse Response(Vehicle v) => new(v.Id, v.Plate, v.Vin, v.Year, v.ModelId, v.ColorId);
    public static async Task<bool> CanRead(AutoCheckDb db, AppActor actor, Vehicle v, CancellationToken ct) => v.OwnerId == actor.Id || await db.Appointments.AnyAsync(x => x.VehicleId == v.Id && ((actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || actor.CenterId == x.CenterId))), ct);
    public static void Configure(RouteHandlerBuilder b) => b.Interactive().Validated<VehicleRequest>(v =>
    {
        v.RuleFor(x => x.Matricula).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$");
        v.RuleFor(x => x.Vin).NotEmpty().Length(17).Matches("^[A-HJ-NPR-Za-hj-npr-z0-9]+$");
        v.RuleFor(x => x.Anio).InclusiveBetween(1900, 2100);
        v.RuleFor(x => x.ModeloId).NotEmpty();
        v.RuleFor(x => x.ColorId).NotEmpty();
    });
    public static async Task CheckCatalog(AutoCheckDb db, VehicleRequest r, CancellationToken ct)
    {
        if (!await db.Catalog.AnyAsync(x => x.Id == r.ModeloId && x.Type == "modelos", ct) || !await db.Catalog.AnyAsync(x => x.Id == r.ColorId && x.Type == "colores", ct))
        {
            throw ApiContract.Invalid("Modelo o color inválido.");
        }
    }

    public static void Apply(Vehicle v, VehicleRequest r)
    {
        v.Plate = r.Matricula.Trim().ToUpperInvariant();
        v.Vin = r.Vin.Trim().ToUpperInvariant();
        v.Year = r.Anio;
        v.ModelId = r.ModeloId;
        v.ColorId = r.ColorId;
    }
}
