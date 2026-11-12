using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ConsultarDisponibilidad;

public static class ConsultarDisponibilidadEndpoint
{
    public static void MapConsultarDisponibilidad(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/disponibilidad", HandleAsync).Document<AvailabilityResponse>("ConsultarDisponibilidad", "RF-CIT-01/03 / PA-12a");
    }

    private static async Task<object> HandleAsync(Guid id, DateOnly fecha, AutoCheckDb db, TimeProvider clock, CancellationToken ct)
    {
        if (fecha == DateOnly.MaxValue)
        {
            throw ApiContract.Invalid("La fecha de disponibilidad está fuera del rango admitido.");
        }

        if (!await db.Centers.AnyAsync(x => x.Id == id && x.Active, ct))
        {
            throw ApiContract.Missing();
        }

        var schedule = await db.Schedules.AsNoTracking().SingleOrDefaultAsync(x => x.CenterId == id && x.Day == (int)fecha.DayOfWeek, ct);
        var now = clock.GetUtcNow();
        if (schedule is null)
        {
            return new AvailabilityResponse([], DominicanTime.Zone.Id);
        }

        var ramps = await db.Ramps.AsNoTracking().Where(x => x.CenterId == id && x.Active).Select(x => x.Id).ToListAsync(ct);
        var start = DominicanTime.Start(fecha);
        var end = start.AddDays(1);
        var blocks = await db.Blocks.AsNoTracking().Where(x => ramps.Contains(x.RampId) && x.Starts >= start && x.Starts < end).ToListAsync(ct);
        return new AvailabilityResponse(BuildBlocks(ramps, schedule, start, now, blocks).ToArray(), DominicanTime.Zone.Id);
    }

    private static IEnumerable<BlockResponse> BuildBlocks(List<Guid> ramps, CenterSchedule schedule, DateTimeOffset start, DateTimeOffset now, List<BookingBlock> blocks)
    {
        var lookup = blocks.ToDictionary(block => (block.RampId, block.Starts));
        foreach (var ramp in ramps)
        {
            for (var minute = schedule.Opens; minute + 30 <= schedule.Closes; minute += 30)
            {
                var at = start.AddMinutes(minute);
                if (at <= now)
                {
                    continue;
                }
                lookup.TryGetValue((ramp, at), out var block);
                yield return DescribeBlock(ramp, at, block, now);
            }
        }
    }

    private static BlockResponse DescribeBlock(Guid ramp, DateTimeOffset at, BookingBlock? block, DateTimeOffset now)
    {
        var state = block is null || (block.State == "RETENIDO" && block.Expires <= now) ? "LIBRE" : block.State;
        return new(ramp, at, state, state == "RETENIDO" ? block!.Expires : null);
    }

}

public sealed record AvailabilityResponse(IReadOnlyList<BlockResponse> Bloques, string ZonaHoraria);
public sealed record BlockResponse(Guid RampaId, DateTimeOffset InicioUtc, string Estado, DateTimeOffset? ExpiraUtc);
