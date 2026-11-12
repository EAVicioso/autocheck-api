using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;

namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public static class SolicitarCertificadoEndpoint
{
    public static void MapSolicitarCertificado(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/inspecciones/{id:guid}/certificado", async (Guid id, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, CertificateIssuer issuer, TimeProvider clock) => await mutation.Run(h, new { id }, async ct =>
        {
            // Same lock order as delivery: outbox row, then inspection. Avoid a retry/issuer deadlock.
            var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM autocheck.\"OutboxMessage\" WHERE \"Kind\" = 'CERTIFICADO' AND \"AggregateId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            await db.LockAsync("inspection:" + id, ct);
            var i = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (i.OwnerId != actor.Id)
            {
                AppActor.Deny();
            }

            var c = await issuer.Reserve(id, ct);
            if (message is null)
            {
                db.Outbox.Add(new() { Kind = "CERTIFICADO", AggregateId = id, Available = clock.GetUtcNow() });
            }
            else if (c.State != "EMITIDO")
            {
                message.Available = clock.GetUtcNow();
            }

            return new ApiReply(c.State == "EMITIDO" ? 200 : 202, ApiReply.Serialize(Response(c)), $"/api/certificados/{c.Id}");
        }, true)).RequireAuthorization().Interactive().WithTags("Certificados").Document<CertificateResponse>("SolicitarCertificado", "CU-06 / PA-06b,c", 202).Produces<CertificateResponse>();
    }
}
