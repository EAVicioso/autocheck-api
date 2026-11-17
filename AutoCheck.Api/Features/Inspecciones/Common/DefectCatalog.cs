// AutoCheck / INTEC, SRS 1.1; EA-06 / PA-05. Version 1.0; history: Git.
namespace AutoCheck.Api.Features.Inspecciones.Common;

public static class DefectCatalog
{
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
}
