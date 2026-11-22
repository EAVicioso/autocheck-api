// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ListarVehiculos;

public sealed record Query(string? Matricula, bool? IncluirArchivados, int? Pagina, int? Tamano, string? Orden, string? Direccion);
