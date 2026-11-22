// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Vehiculos.Common;

public record VehicleRequest(string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId, long? Version = null);
