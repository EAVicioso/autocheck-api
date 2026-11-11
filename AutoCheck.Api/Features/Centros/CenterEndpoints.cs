// AutoCheck academic prototype, SRS 1.1; CU-12, PA-12, RAE-01.
namespace AutoCheck.Api.Features.Centros;

public sealed record CenterRequest(string Nombre, string Direccion, bool Activo = true);
public sealed record RampRequest(string Nombre, bool Activa = true);
public sealed record ScheduleEntry(int Dia, int AperturaMinutos, int CierreMinutos);
public sealed record ScheduleRequest(ScheduleEntry[] Horarios);
public sealed record CenterResponse(Guid Id, string Nombre, string Direccion, bool Activo);
public sealed record RampResponse(Guid Id, Guid CentroId, string Nombre, bool Activa);
public sealed record BlockResponse(Guid RampaId, DateTimeOffset InicioUtc, string Estado, DateTimeOffset? ExpiraUtc);
public sealed record AvailabilityResponse(IReadOnlyList<BlockResponse> Bloques, string ZonaHoraria);
public sealed record CenterDetailResponse(CenterResponse Centro, IReadOnlyList<RampResponse> Rampas, IReadOnlyList<ScheduleEntry> Horarios);

public static class CenterEndpoints
{
    public static CenterResponse Response(Center c) => new(c.Id, c.Name, c.Address, c.Active);
    public static async Task<bool> HasCommitments(AutoCheckDb db, Guid center, Guid? ramp, DateTimeOffset now, CancellationToken ct) =>
        await db.Appointments.AnyAsync(x => x.CenterId == center && (ramp == null || x.RampId == ramp) && (x.State == "EN_INSPECCION" || (x.State == "CONFIRMADA" && x.Starts > now)), ct) ||
        await db.Blocks.AnyAsync(x => x.State == "RETENIDO" && x.Expires > now && (ramp == null || x.RampId == ramp) && db.Ramps.Any(r => r.Id == x.RampId && r.CenterId == center), ct);

    public static void MapCenters(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/centros").WithTags("Centros").RequireAuthorization();
        g.MapGet("", async (HttpContext h, AutoCheckDb db, CancellationToken ct) => new { items = await db.Centers.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Id)
            .Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new CenterResponse(x.Id, x.Name, x.Address, x.Active)).ToListAsync(ct) })
            .Document<ItemsResponse<CenterResponse>>("ListarCentros", "CU-12 / RAE-01");
        g.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, CancellationToken ct) => new
        {
            centro = Response(await db.Centers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing()),
            rampas = await db.Ramps.AsNoTracking().Where(x => x.CenterId == id).Select(x => new RampResponse(x.Id, x.CenterId, x.Name, x.Active)).ToListAsync(ct),
            horarios = await db.Schedules.AsNoTracking().Where(x => x.CenterId == id).OrderBy(x => x.Day).Select(x => new ScheduleEntry(x.Day, x.Opens, x.Closes)).ToListAsync(ct)
        }).Document<CenterDetailResponse>("ObtenerCentro", "CU-12");
        CenterValidation(g.MapPost("", async (CenterRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, ct =>
        {
            actor.RequirePermission(Permissions.Centers); if (actor.CenterId is not null) AppActor.Deny();
            var c = new Center { Name = r.Nombre.Trim(), Address = r.Direccion.Trim(), Active = r.Activo }; db.Centers.Add(c);
            return Task.FromResult(ApiReply.Created($"/api/centros/{c.Id}", Response(c)));
        })).Document<CenterResponse>("CrearCentro", "CU-12 / PA-12c", 201));
        CenterValidation(g.MapPatch("/{id:guid}", async (Guid id, CenterRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers); actor.RequireCenter(id); await db.LockAsync("center:" + id, ct);
            var c = await db.Centers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!r.Activo && await HasCommitments(db, id, null, clock.GetUtcNow(), ct)) throw ApiContract.Conflict("CENTRO_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de desactivar el centro.");
            c.Name = r.Nombre.Trim(); c.Address = r.Direccion.Trim(); c.Active = r.Activo;
            return ApiReply.Ok(Response(c));
        })).Document<CenterResponse>("ActualizarCentro", "PA-12b,c"));
        RampValidation(g.MapPost("/{id:guid}/rampas", async (Guid id, RampRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers); actor.RequireCenter(id); await db.LockAsync("center:" + id, ct);
            if (!await db.Centers.AnyAsync(x => x.Id == id && x.Active, ct)) throw ApiContract.Missing();
            var ramp = new Ramp { CenterId = id, Name = r.Nombre.Trim(), Active = r.Activa }; db.Ramps.Add(ramp);
            return ApiReply.Created($"/api/centros/{id}", new RampResponse(ramp.Id, id, ramp.Name, ramp.Active));
        })).Document<RampResponse>("CrearRampa", "CU-12 / PA-12a", 201));
        RampValidation(g.MapPatch("/{id:guid}/rampas/{rampaId:guid}", async (Guid id, Guid rampaId, RampRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers); actor.RequireCenter(id); await db.LockAsync("center:" + id, ct);
            var ramp = await db.Ramps.SingleOrDefaultAsync(x => x.Id == rampaId && x.CenterId == id, ct) ?? throw ApiContract.Missing();
            if (!r.Activa && await HasCommitments(db, id, rampaId, clock.GetUtcNow(), ct)) throw ApiContract.Conflict("RAMPA_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de desactivar la rampa.");
            ramp.Name = r.Nombre.Trim(); ramp.Active = r.Activa;
            return ApiReply.Ok(new RampResponse(ramp.Id, id, ramp.Name, ramp.Active));
        })).Document<RampResponse>("ActualizarRampa", "PA-12b,c"));
        g.MapPut("/{id:guid}/horarios", async (Guid id, ScheduleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers); actor.RequireCenter(id); await db.LockAsync("center:" + id, ct);
            if (!await db.Centers.AnyAsync(x => x.Id == id, ct)) throw ApiContract.Missing();
            if (await HasCommitments(db, id, null, clock.GetUtcNow(), ct)) throw ApiContract.Conflict("HORARIO_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de cambiar el horario.");
            await db.Schedules.Where(x => x.CenterId == id).ExecuteDeleteAsync(ct);
            db.Schedules.AddRange(r.Horarios.Select(x => new CenterSchedule { CenterId = id, Day = x.Dia, Opens = x.AperturaMinutos, Closes = x.CierreMinutos }));
            return ApiReply.Ok(r);
        })).Interactive().Document<ScheduleRequest>("ConfigurarHorarios", "RAE-01 / PA-12b,c")
        .Validated<ScheduleRequest>(v =>
        {
            v.RuleFor(x => x.Horarios).NotNull().Must(x => x is not null && x.Length <= 7 && x.Select(y => y.Dia).Distinct().Count() == x.Length);
            v.RuleForEach(x => x.Horarios).ChildRules(e => { e.RuleFor(x => x.Dia).InclusiveBetween(0, 6); e.RuleFor(x => x.AperturaMinutos).InclusiveBetween(0, 1410).Must(x => x % 30 == 0); e.RuleFor(x => x.CierreMinutos).InclusiveBetween(30, 1440).Must(x => x % 30 == 0); e.RuleFor(x => x).Must(x => x.CierreMinutos > x.AperturaMinutos); });
        });
        g.MapGet("/{id:guid}/disponibilidad", Availability).Document<AvailabilityResponse>("ConsultarDisponibilidad", "RF-CIT-01/03 / PA-12a");
    }

    private static void CenterValidation(RouteHandlerBuilder b) => b.Interactive().Validated<CenterRequest>(v => { v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120); v.RuleFor(x => x.Direccion).NotEmpty().MaximumLength(300); });
    private static void RampValidation(RouteHandlerBuilder b) => b.Interactive().Validated<RampRequest>(v => v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(80));
    private static async Task<object> Availability(Guid id, DateOnly fecha, AutoCheckDb db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Centers.AnyAsync(x => x.Id == id && x.Active, ct)) throw ApiContract.Missing();
        var schedule = await db.Schedules.AsNoTracking().SingleOrDefaultAsync(x => x.CenterId == id && x.Day == (int)fecha.DayOfWeek, ct);
        var result = new List<BlockResponse>(); var now = clock.GetUtcNow();
        if (schedule is null) return new { bloques = result, zonaHoraria = DominicanTime.Zone.Id };
        var ramps = await db.Ramps.AsNoTracking().Where(x => x.CenterId == id && x.Active).Select(x => x.Id).ToListAsync(ct);
        var start = DominicanTime.Start(fecha); var end = start.AddDays(1);
        var blocks = await db.Blocks.AsNoTracking().Where(x => ramps.Contains(x.RampId) && x.Starts >= start && x.Starts < end).ToListAsync(ct);
        foreach (var ramp in ramps)
            for (var minute = schedule.Opens; minute + 30 <= schedule.Closes; minute += 30)
            {
                var at = start.AddMinutes(minute); if (at <= now) continue;
                var block = blocks.SingleOrDefault(x => x.RampId == ramp && x.Starts == at);
                var state = block is null || (block.State == "RETENIDO" && block.Expires <= now) ? "LIBRE" : block.State;
                result.Add(new(ramp, at, state, state == "RETENIDO" ? block!.Expires : null));
            }
        return new { bloques = result, zonaHoraria = DominicanTime.Zone.Id };
    }
}
