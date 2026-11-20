using AutoCheck.Api.Features.Auditorias.Common;

namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public sealed record Query(DateTimeOffset? DesdeUtc, DateTimeOffset? HastaUtc, string? Operacion, string? Entidad, Guid? ActorId, int? Pagina, int? Tamano, string? Cursor, string? Orden, string? Direccion);
