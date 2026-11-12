using System.Security.Cryptography;
using System.Text;

namespace AutoCheck.Api.Features.Pagos.ConsultarPago;

public static class ConsultarPagoEndpoint
{
    public static void MapConsultarPago(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/intentos/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, PaymentWorkflow workflow, CancellationToken ct) =>
        {
            var p = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (p.UserId != actor.Id)
            {
                AppActor.Deny();
            }

            return await workflow.State(p, ct);
        }).RequireAuthorization().Document<PaymentStateResponse>("ConsultarPago", "CU-04");
    }
}
