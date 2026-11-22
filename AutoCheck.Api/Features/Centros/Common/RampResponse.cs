// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Centros.Common;

public sealed record RampResponse(Guid Id, Guid CentroId, string Nombre, bool Activa);
