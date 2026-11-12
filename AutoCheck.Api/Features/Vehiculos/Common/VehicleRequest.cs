namespace AutoCheck.Api.Features.Vehiculos.Common;

public sealed record VehicleRequest(string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId);
