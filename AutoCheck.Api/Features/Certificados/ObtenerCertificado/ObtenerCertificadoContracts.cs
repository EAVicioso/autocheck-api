// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ObtenerCertificado;

public sealed record Query(Guid Id);
public sealed record Snapshot(CertificateResponse Response, bool Owns);
