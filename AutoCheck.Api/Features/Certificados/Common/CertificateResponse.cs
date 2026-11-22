// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Certificados.Common;

public sealed record CertificateResponse(Guid Id, string Estado, DateTimeOffset EmisionUtc, string HashManifiesto, string? HashPdf);
