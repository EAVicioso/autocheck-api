// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;

namespace AutoCheck.Api.Features.Vehiculos.RegistrarVehiculo;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterVehicleRequest(string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId, long? Version = null)
    : VehicleRequest(Matricula, Vin, Anio, ModeloId, ColorId, Version);

public sealed class RegisterVehicleValidator : VehicleValidator<RegisterVehicleRequest>;

public sealed record Snapshot(Guid OwnerId, long Version, DateTimeOffset? ArchivedAt, bool ActiveOwner, bool CatalogValid);
