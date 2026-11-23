// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Centros.Common;

public sealed record CenterRequest(string Nombre, string Direccion, bool Activo = true);
