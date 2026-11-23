// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Citas.Common;

public sealed record AppointmentResponse(Guid Id, Guid VehiculoId, Guid CentroId, Guid RampaId, Guid? InspectorId, DateTimeOffset InicioUtc, string Estado, Guid? InspeccionAnteriorId);
