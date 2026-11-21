namespace AutoCheck.Api.Features.Vehiculos.ActualizarVehiculo;

public sealed record Snapshot(Guid OwnerId, long Version, DateTimeOffset? ArchivedAt, bool ActiveOwner, bool CatalogValid);
