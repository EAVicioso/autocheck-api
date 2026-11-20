using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public sealed record Query(string? Rol, bool? Activo, bool? IncluirArchivados, string? Nombre, int? Pagina, int? Tamano, string? Orden, string? Direccion);
