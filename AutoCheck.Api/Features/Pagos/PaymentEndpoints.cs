using AutoCheck.Api.Features.Pagos.ConsultarPago;
using AutoCheck.Api.Features.Pagos.IniciarPago;
using AutoCheck.Api.Features.Pagos.RecibirPagoSandbox;

namespace AutoCheck.Api.Features.Pagos;

public static class PaymentEndpoints
{
    public static void MapPayments(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/pagos").WithTags("Pagos");
        g.MapIniciarPago();
        g.MapConsultarPago();
        g.MapRecibirPagoSandbox();
    }
}
