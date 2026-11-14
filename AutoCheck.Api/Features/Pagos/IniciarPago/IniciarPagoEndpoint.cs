using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Features.Pagos.IniciarPago;

public static class IniciarPagoEndpoint
{
    public static void MapIniciarPago(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/intentos", HandleAsync).RequireAuthorization().Interactive().Document<HoldResponse>("IniciarPago", "RF-CIT-01/03 / CU-04 / PA-03a,d", 201).Validated<StartPaymentRequest>(v =>
        {
            v.RuleFor(x => x.VehiculoId).NotEmpty();
            v.RuleFor(x => x.CentroId).NotEmpty();
            v.RuleFor(x => x.RampaId).NotEmpty();
            v.RuleFor(x => x.Escenario).Must(x => new[] { "APROBADO", "RECHAZADO", "ERROR" }.Contains(x));
            v.RuleFor(x => x.DemoraSegundos).InclusiveBetween(0, 600);
        });
    }

    private static async Task<ApiReply> HandleAsync(StartPaymentRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock, IConfiguration config) => await mutation.Run(h, r, async ct =>
    {
        actor.RequireRole(Roles.Citizen);
        await db.LockAsync("center:" + r.CentroId, ct);
        var now = clock.GetUtcNow();
        var amount = config.GetValue("Sandbox:Monto", 1000m);
        if (amount <= 0 || amount > 9999999999.99m || decimal.Round(amount, 2) != amount)
        {
            throw ApiContract.Unavailable();
        }

        if (!await db.Vehicles.AnyAsync(x => x.Id == r.VehiculoId && x.OwnerId == actor.Id, ct))
        {
            AppActor.Deny();
        }

        await ValidateReservation(r, actor.Id, now, db, ct);

        var block = await db.Blocks.SingleOrDefaultAsync(x => x.RampId == r.RampaId && x.Starts == r.InicioUtc.ToUniversalTime(), ct);
        if (block is not null && !(block.State == "RETENIDO" && block.Expires <= now))
        {
            throw ApiContract.Conflict("BLOQUE_NO_DISPONIBLE", "El bloque ya está retenido o confirmado.");
        }

        if (block is not null)
        {
            db.Audit.Add(new() { At = now, Operation = "EXPIRACION", Entity = block.AttemptId.ToString(), Outcome = "VENCIDA", Correlation = h.TraceIdentifier });
        }

        var p = new PaymentAttempt
        {
            UserId = actor.Id,
            VehicleId = r.VehiculoId,
            CenterId = r.CentroId,
            RampId = r.RampaId,
            Starts = r.InicioUtc.ToUniversalTime(),
            Expires = now.AddMinutes(5),
            PreviousInspectionId = r.InspeccionAnteriorId,
            Scenario = r.Escenario,
            Amount = amount
        };
        db.Payments.Add(p);
        if (block is null)
        {
            block = new()
            {
                RampId = r.RampaId,
                Starts = p.Starts
            };
            db.Blocks.Add(block);
        }

        block.AttemptId = p.Id;
        block.State = "RETENIDO";
        block.Expires = p.Expires;
        db.Outbox.Add(new() { Kind = "PAGO_SANDBOX", AggregateId = p.Id, Available = now.AddSeconds(r.DemoraSegundos) });
        db.Outbox.Add(new() { Kind = "DISPONIBILIDAD", AggregateId = Guid.NewGuid(), Available = now, Payload = ApiReply.Serialize(new { centroId = r.CentroId }) });
        return ApiReply.Created($"/api/pagos/intentos/{p.Id}", new HoldResponse(p.Id, p.Id, "RETENIDO", p.Expires, p.Amount, p.Currency));
    }, idempotent: true);

    private static async Task ValidateReservation(StartPaymentRequest r, Guid ownerId, DateTimeOffset now, AutoCheckDb db, CancellationToken ct)
    {
        if (!await db.Centers.AnyAsync(x => x.Id == r.CentroId && x.Active, ct) || !await db.Ramps.AnyAsync(x => x.Id == r.RampaId && x.CenterId == r.CentroId && x.Active, ct))
        {
            throw ApiContract.Invalid("Centro o rampa no disponible.");
        }

        var local = TimeZoneInfo.ConvertTime(r.InicioUtc, DominicanTime.Zone);
        if (r.InicioUtc <= now || r.InicioUtc.UtcTicks % TimeSpan.FromMinutes(30).Ticks != 0)
        {
            throw ApiContract.Invalid("Selecciona un bloque futuro de 30 minutos.");
        }

        var schedule = await db.Schedules.SingleOrDefaultAsync(x => x.CenterId == r.CentroId && x.Day == (int)local.DayOfWeek, ct);
        var minute = local.Hour * 60 + local.Minute;
        if (schedule is null || minute < schedule.Opens || minute + 30 > schedule.Closes)
        {
            throw ApiContract.Invalid("El bloque está fuera del horario del centro.");
        }

        if (r.InspeccionAnteriorId is not null && !await db.Inspections.AnyAsync(x => x.Id == r.InspeccionAnteriorId && x.VehicleId == r.VehiculoId && x.OwnerId == ownerId && x.Result == "Rechazado", ct))
        {
            throw ApiContract.Invalid("La reinspección requiere un rechazo anterior del mismo vehículo.");
        }

    }
}

public sealed record HoldResponse(Guid IntentoId, Guid RetencionId, string Estado, DateTimeOffset ExpiraUtc, decimal Monto, string Moneda);
public sealed record StartPaymentRequest(Guid VehiculoId, Guid CentroId, Guid RampaId, DateTimeOffset InicioUtc, Guid? InspeccionAnteriorId = null, string Escenario = "APROBADO", int DemoraSegundos = 0);
