namespace AutoCheck.Api.Features.Centros.Common;

public sealed record CenterResponse(Guid Id, string Nombre, string Direccion, bool Activo);
