// AutoCheck academic prototype, SRS 1.1; CU-05/10, RF-INS-01, PA-05/10.
using AutoCheck.Api.Features.Vehiculos;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones;

public sealed record StartInspectionRequest(Guid CitaId);
public sealed record RegisterDefectsRequest(int VersionEsperada, DefectEntry[] Defectos);
public sealed record CloseInspectionRequest(int VersionEsperada);
public sealed record NewRuleRequest(int M, DateTimeOffset VigenciaUtc);
public sealed record RuleResponse(Guid Id, string Version, int M, DateTimeOffset VigenciaUtc);
public sealed record HistoryResponse(Guid Id, DateTimeOffset? CierreUtc, string? Resultado, string VersionReglas, int M, int L, int Md, int G, Guid CentroId, Guid? InspeccionAnteriorId);
public sealed record InspectionResponse(Guid Id, Guid VehiculoId, Guid CentroId, Guid RampaId, Guid InspectorId, Guid? InspeccionAnteriorId,
    DateTimeOffset InicioUtc, DateTimeOffset? CierreUtc, string Estado, string? Resultado, string VersionReglas, int M, int L, int Md, int G, int Version, DefectEntry[]? Defectos);

public static class InspectionEndpoints
{
    public static InspectionResponse Response(Inspection i) => new(i.Id, i.VehicleId, i.CenterId, i.RampId, i.InspectorId, i.PreviousId, i.Started, i.Closed, i.State, i.Result,
        i.RuleVersion, i.MaxMinor, i.Minor, i.Medium, i.Serious, i.Revision, i.Defects is null ? null : JsonSerializer.Deserialize<DefectEntry[]>(i.Defects, ApiReply.JsonOptions));
    public static bool CanRead(AppActor a, Inspection i) => a.Id == i.OwnerId || (a.Role == Roles.Inspector && a.CenterId == i.CenterId && a.Id == i.InspectorId) || a.Manages(i.CenterId);
    public static void MapInspections(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/inspecciones").WithTags("Inspecciones").RequireAuthorization();
        g.MapGet("", async (HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) => new
        {
            items = (await db.Inspections.AsNoTracking().Where(x => x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || x.CenterId == a.CenterId)))
                .OrderByDescending(x => x.Started).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response)
        }).Document<ItemsResponse<InspectionResponse>>("ListarInspecciones", "CU-05 / CU-10");
        g.MapPost("", Start).Interactive().Document<InspectionResponse>("IniciarInspeccion", "RF-INS-01 / PA-05b", 201)
            .Validated<StartInspectionRequest>(v => v.RuleFor(x => x.CitaId).NotEmpty());
        g.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var i = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!CanRead(actor, i)) AppActor.Deny(); return Response(i);
        }).Document<InspectionResponse>("ObtenerInspeccion", "CU-05 / CU-10");
        g.MapPut("/{id:guid}/registro", Register).Interactive().Document<InspectionResponse>("RegistrarDefectos", "RF-INS-01 / PA-05c,d")
            .Validated<RegisterDefectsRequest>(v =>
            {
                v.RuleFor(x => x.VersionEsperada).GreaterThanOrEqualTo(0); v.RuleFor(x => x.Defectos).NotNull().Must(x => x is not null && x.Length <= 100);
                v.RuleForEach(x => x.Defectos).ChildRules(d => { d.RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(300); d.RuleFor(x => x.Severidad).Must(x => new[] { "Leve", "Mediano", "Grave" }.Contains(x)); d.RuleFor(x => x.Cantidad).InclusiveBetween(1, 100); });
            });
        g.MapPost("/{id:guid}/cierre", Close).Interactive().Document<InspectionResponse>("CerrarInspeccion", "RF-INS-01 / PA-05a-e")
            .Validated<CloseInspectionRequest>(v => v.RuleFor(x => x.VersionEsperada).GreaterThanOrEqualTo(0));
        g.MapGet("/{id:guid}/reinspecciones", async (Guid id, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            var original = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing(); if (!CanRead(a, original)) AppActor.Deny();
            return new { items = (await db.Inspections.AsNoTracking().Where(x => x.PreviousId == id && (x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || a.CenterId == x.CenterId))))
                .OrderBy(x => x.Started).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).ToListAsync(ct)).Select(Response) };
        }).Document<ItemsResponse<InspectionResponse>>("ConsultarReinspecciones", "Arquitectura DF-05 / CU-10");
        routes.MapGet("/api/vehiculos/{id:guid}/historial", async (Guid id, HttpContext h, AutoCheckDb db, AppActor a, CancellationToken ct) =>
        {
            var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (!await VehicleEndpoints.CanRead(db, a, vehicle, ct)) AppActor.Deny();
            return new { items = await db.Inspections.AsNoTracking().Where(x => x.VehicleId == id && x.Closed != null && (x.OwnerId == a.Id || (a.Role == Roles.Inspector && a.CenterId == x.CenterId && x.InspectorId == a.Id) || (a.Role == Roles.Admin && (a.CenterId == null || x.CenterId == a.CenterId))))
                .OrderByDescending(x => x.Closed).ThenBy(x => x.Id).Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h))
                .Select(x => new { x.Id, cierreUtc = x.Closed, resultado = x.Result, versionReglas = x.RuleVersion, m = x.MaxMinor, l = x.Minor, md = x.Medium, g = x.Serious, centroId = x.CenterId, inspeccionAnteriorId = x.PreviousId }).ToListAsync(ct) };
        }).RequireAuthorization().WithTags("Historial").Document<ItemsResponse<HistoryResponse>>("ConsultarHistorial", "CU-10 / PA-10 / RS-08");
        var rules = routes.MapGroup("/api/configuracion/reglas-inspeccion").WithTags("Reglas").RequireAuthorization();
        rules.MapGet("", async (HttpContext h, AutoCheckDb db, CancellationToken ct) => new { items = await db.Rules.AsNoTracking().OrderByDescending(x => x.Effective)
            .Skip((ApiContract.Page(h) - 1) * ApiContract.Size(h)).Take(ApiContract.Size(h)).Select(x => new { x.Id, version = x.Version, m = x.MaxMinor, vigenciaUtc = x.Effective }).ToListAsync(ct) })
            .Document<ItemsResponse<RuleResponse>>("ConsultarReglas", "RAE-02");
        rules.MapPost("", async (NewRuleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Rules); if (actor.CenterId is not null) AppActor.Deny(); await db.LockAsync("rules", ct);
            if (r.VigenciaUtc < clock.GetUtcNow()) throw ApiContract.Invalid("La vigencia no puede ser retroactiva.");
            var rule = new InspectionRule { MaxMinor = r.M, Effective = r.VigenciaUtc.ToUniversalTime(), ActorId = actor.Id, Version = "V" + (await db.Rules.CountAsync(ct) + 1) };
            db.Rules.Add(rule); return ApiReply.Created("/api/configuracion/reglas-inspeccion", new { rule.Id, version = rule.Version, m = rule.MaxMinor, vigenciaUtc = rule.Effective });
        })).Interactive().Document<RuleResponse>("CrearVersionReglas", "RAE-02 / PA-05b", 201).Validated<NewRuleRequest>(v => v.RuleFor(x => x.M).GreaterThanOrEqualTo(0));
    }

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

    private static async Task<Inspection> Editable(Guid id, int version, AutoCheckDb db, AppActor actor, CancellationToken ct)
    {
        await db.LockAsync("inspection:" + id, ct);
        var i = await db.Inspections.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
        if (actor.Role != Roles.Inspector || i.InspectorId != actor.Id || actor.CenterId != i.CenterId) AppActor.Deny();
        if (i.State != "ABIERTA") throw ApiContract.Conflict("INSPECCION_CERRADA", "La inspección cerrada es inmutable.");
        if (i.Revision != version) throw ApiContract.Conflict("VERSION_DESACTUALIZADA", "Actualiza la inspección antes de guardar.");
        return i;
    }
    private static async Task<ApiReply> Register(Guid id, RegisterDefectsRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct); var counts = InspectionRules.Count(r.Defectos);
        i.Defects = ApiReply.Serialize(r.Defectos); i.Minor = counts.L; i.Medium = counts.Md; i.Serious = counts.G; i.Revision++;
        return ApiReply.Ok(Response(i));
    });
    private static async Task<ApiReply> Close(Guid id, CloseInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock, AutoCheck.Api.Features.Certificados.CertificateIssuer issuer) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct);
        if (i.Defects is null) throw ApiContract.Conflict("INSPECCION_INCOMPLETA", "Registra los defectos, incluso la lista vacía si no hay defectos.");
        i.Result = InspectionRules.Verdict(i.Minor, i.Medium, i.Serious, i.MaxMinor); i.State = "CERRADA"; i.Closed = clock.GetUtcNow(); i.Revision++;
        var appointment = await db.Appointments.SingleAsync(x => x.Id == i.AppointmentId, ct); appointment.State = "COMPLETADA";
        db.Outbox.Add(new() { Kind = "COPIA_MONGO", AggregateId = i.Id, Available = clock.GetUtcNow() });
        if (i.Result == "Aprobado")
        {
            // Reserve identity and manifest durably before any attempt to render/sign the PDF.
            await issuer.Reserve(i.Id, ct);
            db.Outbox.Add(new() { Kind = "CERTIFICADO", AggregateId = i.Id, Available = clock.GetUtcNow() });
        }
        return ApiReply.Ok(Response(i));
    }, true);
}
