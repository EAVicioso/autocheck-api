namespace AutoCheck.Api.Features.Certificados.DescargarCertificado;

public sealed record Query(Guid Id);
public sealed record Metadata(bool Owns, string State);
public sealed record DownloadResponse(byte[] Content, string FileName);
