// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public sealed record Command(Guid Id);
public sealed record Snapshot(Guid OwnerId, string State, string? Result);
