// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.RegistrarActividad;

public sealed class Data(AutoCheckDb db, Mutation mutation, AppActor actor, TimeProvider clock, IHttpContextAccessor accessor) : IData
{
    public async Task Update(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var http = accessor.HttpContext ?? throw new InvalidOperationException("Activity requires an HTTP scope.");
        _ = await mutation.RunTyped<object>(http, new { actor.SessionId }, async token =>
        {
            var session = await db.Sessions.SingleAsync(x => x.Id == actor.SessionId && x.UserId == actor.Id, token);
            if (session.Revoked)
            {
                throw new ApiException(401, ApiErrorCode.SesionExpirada, "La sesión expiró.");
            }
            if (session.LastActivity < clock.GetUtcNow())
            {
                session.LastActivity = clock.GetUtcNow();
            }
            return (204, (object?)null, (string?)null);
        });
    }
}
