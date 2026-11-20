using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Permisos.CambiarAcceso;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record AccessRequest(string Rol, ImmutableArray<string> Permisos, Guid? CentroId, bool Activo, long? Version = null);

public sealed record Snapshot(UserResponse User, bool CenterValid, bool PendingAssignments, bool LastAdministrator);
