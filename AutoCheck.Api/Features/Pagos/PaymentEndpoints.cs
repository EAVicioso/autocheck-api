// AutoCheck academic prototype, SRS 1.1; CU-03/04, PA-03/04.
using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Features.Pagos;

public sealed record StartPaymentRequest(Guid VehiculoId, Guid CentroId, Guid RampaId, DateTimeOffset InicioUtc, Guid? InspeccionAnteriorId = null, string Escenario = "APROBADO", int DemoraSegundos = 0);
public sealed record HoldResponse(Guid IntentoId, Guid RetencionId, string Estado, DateTimeOffset ExpiraUtc, decimal Monto, string Moneda);

public static class PaymentEndpoints
{
    public static void MapPayments(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/pagos").WithTags("Pagos");
        g.MapPost("/intentos", Start).RequireAuthorization().Interactive().Document<HoldResponse>("IniciarPago", "RF-CIT-01/03 / CU-04 / PA-03a,d", 201)
            .Validated<StartPaymentRequest>(v => { v.RuleFor(x => x.VehiculoId).NotEmpty(); v.RuleFor(x => x.CentroId).NotEmpty(); v.RuleFor(x => x.RampaId).NotEmpty(); v.RuleFor(x => x.Escenario).Must(x => new[] { "APROBADO", "RECHAZADO", "ERROR" }.Contains(x)); v.RuleFor(x => x.DemoraSegundos).InclusiveBetween(0, 600); });
        g.MapGet("/intentos/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, PaymentWorkflow workflow, CancellationToken ct) =>
        {
            var p = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (p.UserId != actor.Id) AppActor.Deny(); return await workflow.State(p, ct);
        }).RequireAuthorization().Document<PaymentStateResponse>("ConsultarPago", "CU-04");
        g.MapPost("/webhook", Webhook).AllowAnonymous().RequireRateLimiting("auth").Accepts<SandboxEvent>("application/json").Document<PaymentStateResponse>("RecibirPagoSandbox", "IS-03 / PA-04a,c; X-Sandbox-Signature HMAC-SHA256 required");
    }

    private static async Task<ApiReply> Start(StartPaymentRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock, IConfiguration config) =>
        await mutation.Run(h, r, async ct =>
        {
            actor.RequireRole(Roles.Citizen);
            await db.LockAsync("center:" + r.CentroId, ct);
            var now = clock.GetUtcNow();
            if (!await db.Vehicles.AnyAsync(x => x.Id == r.VehiculoId && x.OwnerId == actor.Id, ct)) AppActor.Deny();
            if (!await db.Centers.AnyAsync(x => x.Id == r.CentroId && x.Active, ct) || !await db.Ramps.AnyAsync(x => x.Id == r.RampaId && x.CenterId == r.CentroId && x.Active, ct)) throw ApiContract.Invalid("Centro o rampa no disponible.");
            var local = TimeZoneInfo.ConvertTime(r.InicioUtc, DominicanTime.Zone);
            if (r.InicioUtc <= now || r.InicioUtc.UtcTicks % TimeSpan.FromMinutes(30).Ticks != 0) throw ApiContract.Invalid("Selecciona un bloque futuro de 30 minutos.");
            var schedule = await db.Schedules.SingleOrDefaultAsync(x => x.CenterId == r.CentroId && x.Day == (int)local.DayOfWeek, ct);
            var minute = local.Hour * 60 + local.Minute;
            if (schedule is null || minute < schedule.Opens || minute + 30 > schedule.Closes) throw ApiContract.Invalid("El bloque está fuera del horario del centro.");
            if (r.InspeccionAnteriorId is not null && !await db.Inspections.AnyAsync(x => x.Id == r.InspeccionAnteriorId && x.VehicleId == r.VehiculoId && x.OwnerId == actor.Id && x.Result == "Rechazado", ct)) throw ApiContract.Invalid("La reinspección requiere un rechazo anterior del mismo vehículo.");
            var block = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == r.RampaId && x.Starts == r.InicioUtc.ToUniversalTime(), ct);
            if (block is not null && !(block.State == "RETENIDO" && block.Expires <= now)) throw ApiContract.Conflict("BLOQUE_NO_DISPONIBLE", "El bloque ya está retenido o confirmado.");
            if (block is not null)
                db.Audit.Add(new() { At = now, Operation = "EXPIRACION", Entity = block.AttemptId.ToString(), Outcome = "VENCIDA", Correlation = h.TraceIdentifier });
            var p = new PaymentAttempt { UserId = actor.Id, VehicleId = r.VehiculoId, CenterId = r.CentroId, RampId = r.RampaId, Starts = r.InicioUtc.ToUniversalTime(), Expires = now.AddMinutes(5),
                PreviousInspectionId = r.InspeccionAnteriorId, Scenario = r.Escenario };
            db.Payments.Add(p);
            if (block is null) { block = new() { RampId = r.RampaId, Starts = p.Starts }; db.Blocks.Add(block); }
            block.AttemptId = p.Id; block.State = "RETENIDO"; block.Expires = p.Expires;
            db.Outbox.Add(new() { Kind = "PAGO_SANDBOX", AggregateId = p.Id, Available = now.AddSeconds(r.DemoraSegundos) });
            db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = now, Payload = ApiReply.Serialize(new { centroId = r.CentroId }) });
            return ApiReply.Created($"/api/pagos/intentos/{p.Id}", new HoldResponse(p.Id, p.Id, "RETENIDO", p.Expires, config.GetValue("Sandbox:Monto", 1000m), "DOP"));
        }, idempotent: true);

    private static async Task<ApiReply> Webhook(HttpContext h, IConfiguration config, AutoCheckDb db, Mutation mutation, PaymentWorkflow workflow)
    {
        var secret = config["Sandbox:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32) throw ApiContract.Unavailable();
        using var reader = new StreamReader(h.Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(h.RequestAborted);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        var signature = h.Request.Headers["X-Sandbox-Signature"].ToString();
        if (signature.Length != 64 || !signature.All(Uri.IsHexDigit))
            throw new ApiException(401, "FIRMA_INVALIDA", "Firma de evento inválida.");
        var supplied = Convert.FromHexString(h.Request.Headers["X-Sandbox-Signature"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied)) throw new ApiException(401, "FIRMA_INVALIDA", "Firma de evento inválida.");
        SandboxEvent e;
        try { e = JsonSerializer.Deserialize<SandboxEvent>(body, ApiReply.JsonOptions) ?? throw ApiContract.Invalid("Evento inválido."); }
        catch (JsonException) { throw ApiContract.Invalid("Evento inválido."); }
        if (string.IsNullOrWhiteSpace(e.EventoId) || e.EventoId.Length > 128 || !new[] { "APROBADO", "RECHAZADO", "ERROR" }.Contains(e.Resultado)) throw ApiContract.Invalid("Evento inválido.");
        return await mutation.Run(h, e, async ct => ApiReply.Ok(await workflow.Apply(e, h.TraceIdentifier, ct)));
    }
}
