// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Certificados.Common;
namespace AutoCheck.Api.Features.Certificados.ListarCertificados;

public sealed record Query(Guid? VehiculoId, string? Estado, DateTimeOffset? DesdeUtc, DateTimeOffset? HastaUtc, int? Pagina, int? Tamano, string? Cursor, string? Orden, string? Direccion);
