namespace AutoCheck.Api.Features.Certificados.SolicitarCertificado;

public sealed record Command(Guid Id);
public sealed record Snapshot(Guid OwnerId, string State, string? Result);
