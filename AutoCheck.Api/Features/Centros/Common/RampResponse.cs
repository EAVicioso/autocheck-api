namespace AutoCheck.Api.Features.Centros.Common;

public sealed record RampResponse(Guid Id, Guid CentroId, string Nombre, bool Activa);
