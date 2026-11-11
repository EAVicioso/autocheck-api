// AutoCheck academic prototype, SRS 1.1; RF-INS-01 / PA-05.
namespace AutoCheck.Api.Features.Inspecciones;

public sealed record DefectEntry(string Descripcion, string Severidad, int Cantidad = 1);
public sealed record DefectCounts(int L, int Md, int G);
public static class InspectionRules
{
    public static string Verdict(int l, int md, int g, int max)
    {
        if (l < 0 || md < 0 || g < 0 || max < 0) throw ApiContract.Invalid("Los conteos y M deben ser enteros no negativos.");
        return g >= 1 || md >= 2 || l > max ? "Rechazado" : "Aprobado";
    }
    public static DefectCounts Count(DefectEntry[] defects)
    {
        if (defects.Any(x => x.Cantidad is < 1 or > 100 || !new[] { "Leve", "Mediano", "Grave" }.Contains(x.Severidad))) throw ApiContract.Invalid("Defectos inválidos.");
        return new(defects.Where(x => x.Severidad == "Leve").Sum(x => x.Cantidad), defects.Where(x => x.Severidad == "Mediano").Sum(x => x.Cantidad), defects.Where(x => x.Severidad == "Grave").Sum(x => x.Cantidad));
    }
}
