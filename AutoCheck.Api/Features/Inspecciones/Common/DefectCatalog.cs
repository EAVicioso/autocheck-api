// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck / INTEC, SRS 1.1; EA-06 / PA-05. Version 1.0; history: Git.
namespace AutoCheck.Api.Features.Inspecciones.Common;

public static class DefectCatalog
{
    public const string CategoryType = "defectos-categorias-v1";
    public const string SubcategoryType = "defectos-subcategorias-v1";

    private static readonly Dictionary<string, string[]> Categories = new(StringComparer.Ordinal)
    {
        ["IDENTIFICACION"] = ["VIN", "MATRICULA"],
        ["ALUMBRADO"] = ["FAROS", "INTERMITENTES"],
        ["FRENOS"] = ["SERVICIO", "ESTACIONAMIENTO"],
        ["DIRECCION"] = ["HOLGURAS", "ASISTENCIA"],
        ["SUSPENSION"] = ["AMORTIGUADORES", "ANCLAJES"],
        ["NEUMATICOS"] = ["DIBUJO", "RUEDAS"],
        ["CHASIS"] = ["ESTRUCTURA", "CARROCERIA"],
        ["EMISIONES"] = ["GASES", "ESCAPE"],
        ["SEGURIDAD"] = ["CINTURONES", "VISIBILIDAD"]
    };

    public static bool Contains(string? category, string? subcategory) => category is not null
        && subcategory is not null && Categories.TryGetValue(category, out var entries)
        && entries.Contains(subcategory, StringComparer.Ordinal);

    public static async Task ValidateReferencesAsync(AutoCheckDb db, DefectEntry[] defects, CancellationToken ct)
    {
        if (defects.Length == 0)
        {
            return;
        }

        var pairs = await (from child in db.Catalog.AsNoTracking()
                           join parent in db.Catalog.AsNoTracking() on child.ParentId equals (Guid?)parent.Id
                           where child.Type == SubcategoryType && parent.Type == CategoryType
                           select new { Category = parent.Name, Subcategory = child.Name }).ToListAsync(ct);
        var registered = pairs.Select(x => (x.Category, x.Subcategory)).ToHashSet();
        if (defects.Any(x => !registered.Contains((x.Categoria!, x.Subcategoria!))))
        {
            throw ApiContract.Invalid("Completa los defectos con categorías y subcategorías vigentes del catálogo V1.");
        }
    }
}
