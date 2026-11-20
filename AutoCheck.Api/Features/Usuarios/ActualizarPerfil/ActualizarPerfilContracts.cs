using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileRequest(string Nombre, string Celular, long? Version = null);

public sealed record Snapshot(long Version, DateTimeOffset? ArchivedAt);
