namespace AutoCheck.Api.Features.Vehiculos.Common;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record VehicleRequest(string Matricula, string Vin, int Anio, Guid ModeloId, Guid ColorId, long? Version = null);
