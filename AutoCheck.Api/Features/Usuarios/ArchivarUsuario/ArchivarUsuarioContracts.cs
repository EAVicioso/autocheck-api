// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Usuarios.ArchivarUsuario;

public sealed record Command(Guid Id, long Version);
public sealed record Snapshot(Guid? CenterId, long Version, DateTimeOffset? ArchivedAt, bool Pending, bool LastAdministrator);
