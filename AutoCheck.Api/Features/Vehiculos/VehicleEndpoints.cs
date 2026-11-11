// AutoCheck academic prototype, SRS 1.1; IU-01, CU-10, PA-10.
namespace AutoCheck.Api.Features.Vehiculos;

public sealed record VehicleRequest(string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId);
public sealed record VehicleResponse(Guid Id, string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId);
public sealed record CatalogResponse(Guid Id, string Nombre, Guid? PadreId);

public static class VehicleEndpoints
{
    public static VehicleResponse Response(Vehicle v) => new(v.Id, v.Plate, v.Vin, v.Year, v.ModelId, v.ColorId);
    public static async Task<bool> CanRead(AutoCheckDb db, AppActor actor, Vehicle v, CancellationToken ct) =>
        v.OwnerId == actor.Id || await db.Appointments.AnyAsync(x => x.VehicleId == v.Id &&
            ((actor.Role == Roles.Inspector && actor.CenterId == x.CenterId && x.InspectorId == actor.Id) || (actor.Role == Roles.Admin && (actor.CenterId == null || actor.CenterId == x.CenterId))), ct);

    public static void MapVehicles(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/catalogos/{tipo}", async (string tipo, HttpContext h, AutoCheckDb db, CancellationToken ct) =>
        {
            if (!new[] { "marcas", "modelos", "colores", "categorias" }.Contains(tipo)) throw ApiContract.Missing();
            return new { items = await db.Catalog.AsNoTracking().Where(x => x.Type == tipo).OrderBy(x => x.Name).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h))
                .Select(x => new { x.Id, nombre = x.Name, padreId = x.ParentId }).ToListAsync(ct) };
        }).RequireAuthorization().WithTags("Vehiculos").Document<ItemsResponse<CatalogResponse>>("ConsultarCatalogo", "IU-01");
        var g = routes.MapGroup("/api/vehiculos").WithTags("Vehiculos").RequireAuthorization();
        g.MapGet("", async (HttpContext h, AutoCheckDb db, AppActor actor, CancellationToken ct) => new
        {
            items = (await db.Vehicles.AsNoTracking().Where(x => x.OwnerId == actor.Id).OrderBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
        }).Document<ItemsResponse<VehicleResponse>>("ListarVehiculos", "IU-01 / PA-10a");
        g.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!await CanRead(db, actor, vehicle, ct)) AppActor.Deny();
            return Response(vehicle);
        }).Document<VehicleResponse>("ObtenerVehiculo", "CU-10 / PA-10a");
        Configure(g.MapPost("", async (VehicleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequireRole(Roles.Citizen); await CheckCatalog(db, r, ct);
            var v = new Vehicle { OwnerId = actor.Id }; Apply(v, r); db.Vehicles.Add(v);
            return ApiReply.Created($"/api/vehiculos/{v.Id}", Response(v));
        })).Document<VehicleResponse>("RegistrarVehiculo", "IU-01", 201));
        Configure(g.MapPatch("/{id:guid}", async (Guid id, VehicleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            await db.LockAsync("vehicle:" + id, ct);
            var v = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (v.OwnerId != actor.Id) AppActor.Deny();
            await CheckCatalog(db, r, ct); Apply(v, r);
            return ApiReply.Ok(Response(v));
        })).Document<VehicleResponse>("ActualizarVehiculo", "IU-01 / PA-05d"));
    }
    private static void Configure(RouteHandlerBuilder b) => b.Interactive().Validated<VehicleRequest>(v =>
    {
        v.RuleFor(x => x.Matricula).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$");
        v.RuleFor(x => x.Vin).NotEmpty().Length(17).Matches("^[A-HJ-NPR-Za-hj-npr-z0-9]+$");
        v.RuleFor(x => x.Anio).InclusiveBetween(1900, 2100); v.RuleFor(x => x.ModeloId).NotEmpty(); v.RuleFor(x => x.ColorId).NotEmpty();
    });
    private static async Task CheckCatalog(AutoCheckDb db, VehicleRequest r, CancellationToken ct)
    {
        if (!await db.Catalog.AnyAsync(x => x.Id == r.ModeloId && x.Type == "modelos", ct) || !await db.Catalog.AnyAsync(x => x.Id == r.ColorId && x.Type == "colores", ct)) throw ApiContract.Invalid("Modelo o color inválido.");
    }
    private static void Apply(Vehicle v, VehicleRequest r) { v.Plate = r.Matricula.Trim().ToUpperInvariant(); v.Vin = r.Vin.Trim().ToUpperInvariant(); v.Year = r.Anio; v.ModelId = r.ModeloId; v.ColorId = r.ColorId; }
}
