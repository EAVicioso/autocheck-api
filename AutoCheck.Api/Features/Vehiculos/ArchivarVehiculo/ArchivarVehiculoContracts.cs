namespace AutoCheck.Api.Features.Vehiculos.ArchivarVehiculo;

public sealed record Command(Guid Id, long Version);
public sealed record Snapshot(Guid OwnerId, long Version, DateTimeOffset? ArchivedAt, bool Pending);
