// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.
using AutoCheck.Api.Features.Vehiculos;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones;

public sealed record CloseInspectionRequest(int VersionEsperada);

public static partial class InspectionEndpoints
{
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
