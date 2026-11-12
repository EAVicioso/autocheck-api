// AutoCheck academic prototype, SRS 1.1; CU-03/04, PA-03/04.
using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Features.Pagos;

public sealed record HoldResponse(Guid IntentoId, Guid RetencionId, string Estado, DateTimeOffset ExpiraUtc, decimal Monto, string Moneda);

public static partial class PaymentEndpoints
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


}
