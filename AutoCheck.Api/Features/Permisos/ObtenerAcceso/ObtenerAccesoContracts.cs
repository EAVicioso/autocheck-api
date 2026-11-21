namespace AutoCheck.Api.Features.Permisos.ObtenerAcceso;

public sealed record Query(Guid Id);
public sealed record AccessResponse(Guid UsuarioId, string Rol, ImmutableArray<string> Permisos, Guid? CentroId, bool Activo, long Version, DateTimeOffset? ArchivadoUtc);
