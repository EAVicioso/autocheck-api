namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public sealed record Query(string Tipo, Guid? PadreId, int? Pagina, int? Tamano, string? Orden, string? Direccion);
public sealed record CatalogResponse(Guid Id, string Nombre, Guid? PadreId);
