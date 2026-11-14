using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.CerrarInspeccion;

public static class CerrarInspeccionEndpoint
{
    public static void MapCerrarInspeccion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/{id:guid}/cierre", HandleAsync).Interactive().Document<InspectionResponse>("CerrarInspeccion", "RF-INS-01 / PA-05a-e").Validated<CloseInspectionRequest>(v => v.RuleFor(x => x.VersionEsperada).GreaterThanOrEqualTo(0));
    }

    private static async Task<ApiReply> HandleAsync(Guid id, CloseInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock, AutoCheck.Api.Features.Certificados.CertificateIssuer issuer) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct);
        if (i.Defects is null)
        {
            throw ApiContract.Conflict("INSPECCION_INCOMPLETA", "Registra los defectos, incluso la lista vacía si no hay defectos.");
        }

        i.Result = InspectionRules.Verdict(i.Minor, i.Medium, i.Serious, i.MaxMinor);
        i.State = "CERRADA";
        i.Closed = clock.GetUtcNow();
        i.Revision++;
        var appointment = await db.Appointments.SingleAsync(x => x.Id == i.AppointmentId, ct);
        appointment.State = "COMPLETADA";
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

public sealed record CloseInspectionRequest(int VersionEsperada);
