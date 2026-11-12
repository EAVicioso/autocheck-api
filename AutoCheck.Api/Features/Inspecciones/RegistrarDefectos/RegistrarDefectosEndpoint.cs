using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.RegistrarDefectos;

public static class RegistrarDefectosEndpoint
{
    public static void MapRegistrarDefectos(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/registro", HandleAsync).Interactive().Document<InspectionResponse>("RegistrarDefectos", "RF-INS-01 / PA-05c,d").Validated<RegisterDefectsRequest>(v =>
        {
            v.RuleFor(x => x.VersionEsperada).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x.Defectos).NotNull().Must(x => x is not null && x.Length <= 100);
            v.RuleForEach(x => x.Defectos).NotNull().ChildRules(d =>
            {
                d.RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(300);
                d.RuleFor(x => x.Severidad).Must(x => new[] { "Leve", "Mediano", "Grave" }.Contains(x));
                d.RuleFor(x => x.Cantidad).InclusiveBetween(1, 100);
            });
        });
    }

    private static async Task<ApiReply> HandleAsync(Guid id, RegisterDefectsRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct);
        var counts = InspectionRules.Count(r.Defectos);
        i.Defects = ApiReply.Serialize(r.Defectos);
        i.Minor = counts.L;
        i.Medium = counts.Md;
        i.Serious = counts.G;
        i.Revision++;
        return ApiReply.Ok(Response(i));
    });
}

public sealed record RegisterDefectsRequest(int VersionEsperada, DefectEntry[] Defectos);
