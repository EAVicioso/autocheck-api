using AutoCheck.Api.Features.Centros.Common;
using static AutoCheck.Api.Features.Centros.Common.CenterSupport;

namespace AutoCheck.Api.Features.Centros.ConfigurarHorarios;

public static class ConfigurarHorariosEndpoint
{
    public static void MapConfigurarHorarios(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/{id:guid}/horarios", async (Guid id, ScheduleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Centers);
            actor.RequireCenter(id);
            await db.LockAsync("center:" + id, ct);
            if (!await db.Centers.AnyAsync(x => x.Id == id, ct))
            {
                throw ApiContract.Missing();
            }

            if (await HasCommitments(db, id, null, clock.GetUtcNow(), ct))
            {
                throw ApiContract.Conflict("HORARIO_CON_COMPROMISOS", "Resuelve las citas y retenciones antes de cambiar el horario.");
            }

            await db.Schedules.Where(x => x.CenterId == id).ExecuteDeleteAsync(ct);
            db.Schedules.AddRange(r.Horarios.Select(x => new CenterSchedule { CenterId = id, Day = x.Dia, Opens = x.AperturaMinutos, Closes = x.CierreMinutos }));
            return ApiReply.Ok(r);
        })).Interactive().Document<ScheduleRequest>("ConfigurarHorarios", "RAE-01 / PA-12b,c").Validated<ScheduleRequest>(v =>
        {
            v.RuleFor(x => x.Horarios).NotNull().Must(x => x is not null && x.Length <= 7 && x.All(entry => entry is not null) && x.Select(y => y.Dia).Distinct().Count() == x.Length);
            v.RuleForEach(x => x.Horarios).NotNull().ChildRules(e =>
            {
                e.RuleFor(x => x.Dia).InclusiveBetween(0, 6);
                e.RuleFor(x => x.AperturaMinutos).InclusiveBetween(0, 1410).Must(x => x % 30 == 0);
                e.RuleFor(x => x.CierreMinutos).InclusiveBetween(30, 1440).Must(x => x % 30 == 0);
                e.RuleFor(x => x).Must(x => x.CierreMinutos > x.AperturaMinutos);
            });
        });
    }
}

public sealed record ScheduleRequest(ScheduleEntry[] Horarios);
