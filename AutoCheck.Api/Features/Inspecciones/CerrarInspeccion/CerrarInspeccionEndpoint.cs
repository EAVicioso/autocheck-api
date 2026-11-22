// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1; Section C / PA-05/08/09/10. Version 1.0; history: Git.
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

    private static async Task<ApiReply> HandleAsync(Guid id, CloseInspectionRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock, AutoCheck.Api.Features.Certificados.CertificateIssuer issuer)
    {
        actor.RequireRole(Roles.Inspector);
        var assignment = await db.Inspections.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.InspectorId, x.CenterId }).SingleOrDefaultAsync(h.RequestAborted) ?? throw ApiContract.Missing();
        if (assignment.InspectorId != actor.Id || assignment.CenterId != actor.CenterId)
        {
            AppActor.Deny();
        }
        return await mutation.Run(h, r, async ct =>
        {
            var i = await Editable(id, r.VersionEsperada, db, actor, ct);
            if (i.Defects is null)
            {
                throw ApiContract.Invalid("Registra los defectos, incluso la lista vacía si no hay defectos.");
            }

            var defects = await ReadCompleteDefects(i.Defects, db, ct);
            var counts = InspectionRules.Count(defects);
            (i.Minor, i.Medium, i.Serious) = counts;
            i.Result = InspectionRules.Verdict(counts.L, counts.Md, counts.G, i.MaxMinor);
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

    private static async Task<DefectEntry[]> ReadCompleteDefects(string json, AutoCheckDb db, CancellationToken ct)
    {
        const string incomplete = "Completa o reemplaza los defectos registrados antes de cerrar la inspección.";
        DefectEntry[]? defects;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 100
                || document.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("cantidad", out _)))
            {
                throw ApiContract.Invalid(incomplete);
            }
            defects = document.RootElement.Deserialize<DefectEntry[]>(ApiReply.JsonOptions);
        }
        catch (JsonException)
        {
            throw ApiContract.Invalid(incomplete);
        }

        if (defects is null || defects.Any(x => x is null || string.IsNullOrWhiteSpace(x.Descripcion)
            || x.Descripcion.Length > 300 || !DefectCatalog.Contains(x.Categoria, x.Subcategoria)))
        {
            throw ApiContract.Invalid(incomplete);
        }
        await DefectCatalog.ValidateReferencesAsync(db, defects, ct);
        return defects;
    }
}

public sealed record CloseInspectionRequest(int VersionEsperada);
