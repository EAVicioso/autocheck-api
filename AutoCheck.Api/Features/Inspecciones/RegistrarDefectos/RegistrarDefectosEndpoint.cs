// AutoCheck / INTEC, SRS 1.1; Section C / PA-05/08/09/10. Version 1.0; history: Git.
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
                d.RuleFor(x => x.Severidad).Must(InspectionRules.ValidSeverity);
                d.RuleFor(x => x.Cantidad).InclusiveBetween(1, 100);
                d.RuleFor(x => x).Must(x => DefectCatalog.Contains(x.Categoria, x.Subcategoria)).WithMessage("Categoría/subcategoría inválida según el catálogo académico V1.");
            });
        });
    }

    private static async Task<ApiReply> HandleAsync(Guid id, RegisterDefectsRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation) => await mutation.Run(h, r, async ct =>
    {
        var i = await Editable(id, r.VersionEsperada, db, actor, ct);
        await DefectCatalog.ValidateReferencesAsync(db, r.Defectos, ct);
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
