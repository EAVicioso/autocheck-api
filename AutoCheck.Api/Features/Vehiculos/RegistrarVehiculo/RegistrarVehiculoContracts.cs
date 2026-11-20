namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

public sealed record Snapshot(Guid OwnerId, long Version, DateTimeOffset? ArchivedAt, bool ActiveOwner, bool CatalogValid);
