// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auditorias.Common;

namespace AutoCheck.Api.Features.Auditorias.ConsultarAuditorias;

public sealed record Query([Microsoft.AspNetCore.Mvc.FromQuery(Name = "desdeUtc")] DateTimeOffset? DesdeUtc, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hastaUtc")] DateTimeOffset? HastaUtc, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "operacion")] string? Operacion, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "entidad")] string? Entidad, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "actorId")] Guid? ActorId, int? Pagina, int? Tamano, string? Cursor, string? Orden, string? Direccion);
