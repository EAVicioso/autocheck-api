namespace AutoCheck.Api.Features.Vehiculos.Common;

public sealed record VehicleResponse(Guid Id, string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId);
