// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;

namespace AutoCheck.Api.Features.Vehiculos.Common;

public static class VehicleSupport
{
    public static VehicleResponse Response(Vehicle v) => new(v.Id, v.Plate, v.Vin, v.Year, v.ModelId, v.ColorId, v.Version, v.ArchivedAt);
    public static async Task<bool> CanRead(AutoCheckDb db, AppActor actor, Vehicle v, CancellationToken ct) => v.OwnerId == actor.Id || await db.Appointments.AnyAsync(x => x.VehicleId == v.Id && ((actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || actor.CenterId == x.CenterId))), ct);
    public static void Configure(RouteHandlerBuilder b) => b.Interactive().Validated<VehicleRequest>();
    public static async Task<bool> CatalogValid(AutoCheckDb db, VehicleRequest request, CancellationToken ct) => await db.Catalog.AnyAsync(x => x.Id == request.ModeloId && x.Type == "modelos", ct) && await db.Catalog.AnyAsync(x => x.Id == request.ColorId && x.Type == "colores", ct);
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
