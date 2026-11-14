using AutoCheck.Api.Features.Inspecciones.Common;
using AutoCheck.Api.Features.Vehiculos;
using AutoCheck.Api.Features.Vehiculos.Common;
using MongoDB.Bson;
using static AutoCheck.Api.Features.Inspecciones.Common.InspectionSupport;

namespace AutoCheck.Api.Features.Inspecciones.CrearVersionReglas;

public static class CrearVersionReglasEndpoint
{
    public static void MapCrearVersionReglas(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("", async (NewRuleRequest r, HttpContext h, AutoCheckDb db, AppActor actor, Mutation mutation, TimeProvider clock) => await mutation.Run(h, r, async ct =>
        {
            actor.RequirePermission(Permissions.Rules);
            if (actor.CenterId is not null)
            {
                AppActor.Deny();
            }

            await db.LockAsync("rules", ct);
            if (r.VigenciaUtc < clock.GetUtcNow())
            {
                throw ApiContract.Invalid("La vigencia no puede ser retroactiva.");
            }

            var effective = r.VigenciaUtc.ToUniversalTime();
            var previous = await db.Rules.Where(x => x.Effective <= effective).OrderByDescending(x => x.Effective).Select(x => new { version = x.Version, m = x.MaxMinor }).FirstAsync(ct);
            var rule = new InspectionRule
            {
                MaxMinor = r.M,
                Effective = r.VigenciaUtc.ToUniversalTime(),
                ActorId = actor.Id,
                Version = "V" + (await db.Rules.CountAsync(ct) + 1)
            };
            db.Audit.Add(new() { ActorId = actor.Id, At = clock.GetUtcNow(), Operation = "CAMBIO_REGLAS", Entity = rule.Id.ToString(), Correlation = h.TraceIdentifier, Outcome = ApiReply.Serialize(new { anterior = previous, nuevo = new { version = rule.Version, m = rule.MaxMinor, vigenciaUtc = rule.Effective } }) });
            db.Rules.Add(rule);
            return ApiReply.Created("/api/configuracion/reglas-inspeccion", new { rule.Id, version = rule.Version, m = rule.MaxMinor, vigenciaUtc = rule.Effective });
        })).Interactive().Document<RuleResponse>("CrearVersionReglas", "RAE-02 / PA-05b", 201).Validated<NewRuleRequest>(v => v.RuleFor(x => x.M).GreaterThanOrEqualTo(0));
    }
}

public sealed record NewRuleRequest(int M, DateTimeOffset VigenciaUtc);
