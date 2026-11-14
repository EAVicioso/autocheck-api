using AutoCheck.Api.Features.Reportes;
using AutoCheck.Api.Features.Reportes.Common;

namespace AutoCheck.Api.Features.Reportes.Common;

public static class ReportSupport
{
    public static (DateOnly, DateOnly) Dates(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var a = from ?? DominicanTime.Date(clock.GetUtcNow());
        var b = to ?? a;
        if (b < a || b.DayNumber - a.DayNumber > 366)
        {
            throw ApiContract.Invalid("Rango de fechas inválido; máximo 366 días.");
        }

        return (a, b);
    }
}
