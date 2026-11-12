// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.
using AutoCheck.Api.Features.Vehiculos;
using MongoDB.Bson;

namespace AutoCheck.Api.Features.Inspecciones;

public sealed record RegisterDefectsRequest(int VersionEsperada, DefectEntry[] Defectos);

public static partial class InspectionEndpoints
{
    private static async Task<ApiReply> Register(Guid id, RegisterDefectsRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct); var counts = InspectionRules.Count(r.Defectos);
        i.Defects = ApiReply.Serialize(r.Defectos); i.Minor = counts.L; i.Medium = counts.Md; i.Serious = counts.G; i.Revision++;
        return ApiReply.Ok(Response(i));
    });
}
