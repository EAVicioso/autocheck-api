// AutoCheck academic prototype, SRS 1.1; CU-06/07, PA-06/07.
namespace AutoCheck.Api.Features.Certificados;

public sealed record CertificateResponse(Guid Id, string Estado, DateTimeOffset EmisionUtc, string HashManifiesto, string? HashPdf);
public sealed record VerificationResponse(Guid CertificadoId, DateTimeOffset EmisionUtc, string Centro, string Resultado, string Integridad);
public static class CertificateEndpoints
{
    public static CertificateResponse Response(Certificate c) => new(c.Id, c.State, c.Issued, c.ManifestHash, c.PdfHash);
    public static void MapCertificates(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/inspecciones/{id:guid}/certificado", async (Guid id, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, CertificateIssuer issuer, TimeProvider clock) =>
            await mutation.Run(h, new { id }, async ct =>
            {
                await db.LockAsync("inspection:" + id, ct);
                var i = await db.Inspections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
                if (i.OwnerId != actor.Id) AppActor.Deny();
                var c = await issuer.Reserve(id, ct);
                var message = await db.Outbox.SingleOrDefaultAsync(x => x.Kind == "CERTIFICADO" && x.AggregateId == id, ct);
                if (message is null) db.Outbox.Add(new() { Kind = "CERTIFICADO", AggregateId = id, Available = clock.GetUtcNow() });
                else if (c.State != "EMITIDO") message.Available = clock.GetUtcNow();
                return new ApiReply(c.State == "EMITIDO" ? 200 : 202, ApiReply.Serialize(Response(c)), $"/api/certificados/{c.Id}");
            }, true)).RequireAuthorization().Interactive().WithTags("Certificados").Document<CertificateResponse>("SolicitarCertificado", "CU-06 / PA-06b,c", 202).Produces<CertificateResponse>();
        var g = routes.MapGroup("/api/certificados").WithTags("Certificados");
        g.MapGet("/{id:guid}", async (Guid id, AutoCheckDb db, AppActor actor, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.OwnerId, x.State, x.Issued, x.ManifestHash, x.PdfHash }).SingleOrDefaultAsync(ct) ?? throw ApiContract.Missing();
            if (c.OwnerId != actor.Id) AppActor.Deny(); return new CertificateResponse(c.Id, c.State, c.Issued, c.ManifestHash, c.PdfHash);
        }).RequireAuthorization().Document<CertificateResponse>("ObtenerCertificado", "CU-06");
        g.MapGet("/{id:guid}/pdf", async (Guid id, AutoCheckDb db, AppActor actor, HttpContext h, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiContract.Missing();
            if (c.OwnerId != actor.Id) AppActor.Deny();
            if (c.State != "EMITIDO" || c.Pdf is null) throw ApiContract.Conflict("CERTIFICADO_PENDIENTE", "El certificado aún no está disponible.");
            h.Response.Headers.CacheControl = "private, no-store";
            return Results.File(c.Pdf, "application/pdf", $"AutoCheck-{c.Id}.pdf", enableRangeProcessing: false);
        }).RequireAuthorization().WithName("DescargarCertificado").WithSummary("CU-06 / PA-06a,c").Produces(200, contentType: "application/pdf").ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        g.MapGet("/{id:guid}/verificacion", async (Guid id, string hash, AutoCheckDb db, CancellationToken ct) =>
        {
            var c = await db.Certificates.AsNoTracking().Where(x => x.Id == id && x.State == "EMITIDO").Select(x => new { x.Id, x.InspectionId, x.Issued, x.Manifest, x.ManifestHash }).SingleOrDefaultAsync(ct)
                ?? throw new ApiException(404, "CERTIFICADO_NO_ENCONTRADO", "Certificado no encontrado.");
            if (hash != c.ManifestHash || ApiContract.Hash(c.Manifest) != c.ManifestHash) throw ApiContract.Conflict("INTEGRIDAD_NO_VALIDA", "Integridad no válida.");
            var i = await db.Inspections.AsNoTracking().Where(x => x.Id == c.InspectionId).Select(x => new { x.CenterSnapshot, x.Result }).SingleAsync(ct);
            using var center = JsonDocument.Parse(i.CenterSnapshot);
            return new { certificadoId = c.Id, emisionUtc = c.Issued, centro = center.RootElement.GetProperty("nombre").GetString(), resultado = i.Result, integridad = "VALIDA" };
        }).AllowAnonymous().RequireRateLimiting("public").Document<VerificationResponse>("VerificarCertificado", "CU-07 / RF-CER-01 / PA-07a,b");
    }
}
