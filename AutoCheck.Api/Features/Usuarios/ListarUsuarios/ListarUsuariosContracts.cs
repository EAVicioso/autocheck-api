using AutoCheck.Api.Features.Usuarios.Common;
namespace AutoCheck.Api.Features.Usuarios.ListarUsuarios;

public sealed record Query([Microsoft.AspNetCore.Mvc.FromQuery(Name = "rol")] string? Rol, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "activo")] bool? Activo, bool? IncluirArchivados, string? Nombre, int? Pagina, int? Tamano, string? Orden, string? Direccion);
