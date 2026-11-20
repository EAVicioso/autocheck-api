namespace AutoCheck.Api.Features.Certificados.VerificarCertificado;

public sealed record Query(Guid Id, string Hash);
public sealed record Snapshot(Guid Id, DateTimeOffset Issued, string Manifest, string Hash, string Center, string Result);
public sealed record VerificationResponse(Guid CertificadoId, DateTimeOffset EmisionUtc, string Centro, string Resultado, string Integridad);
