namespace AutoCheck.Api.Features.Usuarios.Common;

public sealed record UserResponse(Guid Id, string Correo, string Nombre, string Celular, string Rol, string[] Permisos, Guid? CentroId, bool Activo);
