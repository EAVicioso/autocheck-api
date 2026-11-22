// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Vehiculos.Common;
namespace AutoCheck.Api.Features.Vehiculos.ObtenerVehiculo;

public sealed record Query(Guid Id);
public sealed record Snapshot(VehicleResponse Response, bool CanRead);
