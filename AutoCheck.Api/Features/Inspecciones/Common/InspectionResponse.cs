using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones.Common;

public sealed record InspectionResponse(Guid Id, Guid VehiculoId, Guid CentroId, Guid RampaId, Guid InspectorId, Guid? InspeccionAnteriorId, DateTimeOffset InicioUtc, DateTimeOffset? CierreUtc, string Estado, string? Resultado, string VersionReglas, int M, int L, int Md, int G, int Version, DefectEntry[]? Defectos);
