using AutoCheck.Api.Features.Certificados.Common;
using static AutoCheck.Api.Features.Certificados.Common.CertificateSupport;
namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public sealed class Data(AutoCheckDb db, Mutation mutation, CertificateIssuer issuer, TimeProvider clock) : IData
{
    public Task<(int Status, CertificateResponse? Body, string? Location)> Execute(Guid id, HttpContext h, Action<Snapshot> validate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return mutation.RunTyped<CertificateResponse>(h, new { id }, async ct =>
        {
            // Same lock order as delivery: outbox row, then inspection. Avoid a retry/issuer deadlock.
            var message = await db.Outbox.FromSqlInterpolated($"SELECT * FROM autocheck.\"OutboxMessage\" WHERE \"Kind\" = 'CERTIFICADO' AND \"AggregateId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            await db.LockAsync("inspection:" + id, ct);
            var i = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            validate(new Snapshot(i.OwnerId, i.State, i.Result));

            var c = await issuer.Reserve(id, ct);
            if (message is null)
            {
                db.Outbox.Add(new() { Kind = "CERTIFICADO", AggregateId = id, Available = clock.GetUtcNow() });
            }
            else if (c.State != "EMITIDO")
            {
                message.Available = clock.GetUtcNow();
            }

            return (c.State == "EMITIDO" ? 200 : 202, Response(c), $"/api/certificados/{c.Id}");
        }, true);
    }

}
