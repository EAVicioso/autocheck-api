using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones.Common;

public sealed record RuleResponse(Guid Id, string Version, int M, DateTimeOffset VigenciaUtc);
