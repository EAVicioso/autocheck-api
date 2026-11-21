namespace AutoCheck.Api.Features.Vehiculos.ConsultarCatalogo;

public sealed record Query(string Tipo, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "padreId")] Guid? PadreId, int? Pagina, int? Tamano, string? Orden, string? Direccion);
public sealed record CatalogResponse(Guid Id, string Nombre, Guid? PadreId);
