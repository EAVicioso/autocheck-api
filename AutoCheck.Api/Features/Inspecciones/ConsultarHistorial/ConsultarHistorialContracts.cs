namespace AutoCheck.Api.Features.Inspecciones.ConsultarHistorial;

public sealed record Query(Guid Id, string? Resultado, DateOnly? Desde, DateOnly? Hasta, int? Pagina, int? Tamano, string? Cursor, string? Orden, string? Direccion);
public sealed record HistoryResponse(Guid Id, DateTimeOffset? CierreUtc, string? Resultado, string VersionReglas, int M, int L, int Md, int G, Guid CentroId, Guid? InspeccionAnteriorId);
