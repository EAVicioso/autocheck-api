// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public sealed record Query(Guid Id, string? Resultado, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "desde")] DateOnly? Desde, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hasta")] DateOnly? Hasta, int? Pagina, int? Tamano, string? Cursor, string? Orden, string? Direccion, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "estado")] string? Estado = null);
public sealed record HistoryResponse(Guid Id, DateTimeOffset? CierreUtc, string? Resultado, string VersionReglas, int M, int L, int Md, int G, Guid CentroId, Guid? InspeccionAnteriorId);
