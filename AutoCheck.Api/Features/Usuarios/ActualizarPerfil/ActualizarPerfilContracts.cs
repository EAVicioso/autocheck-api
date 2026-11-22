// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public sealed record ProfileRequest(string Nombre, string Celular, long? Version = null);

public sealed record Snapshot(long Version, DateTimeOffset? ArchivedAt);
