namespace AutoCheck.Api.Features.Certificados.Common;

public sealed record CertificateResponse(Guid Id, string Estado, DateTimeOffset EmisionUtc, string HashManifiesto, string? HashPdf);
