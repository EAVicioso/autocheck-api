using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;

namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public static class RestablecerContrasenaEndpoint
{
    public static void MapRestablecerContrasena(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/restablecimiento", async (ResetRequest r, IIdentityProvider provider, AutoCheckDb db, TimeProvider clock, HttpContext http, CancellationToken ct) =>
        {
            var authId = await provider.Reset(r.Token, r.NuevaContrasena, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var userId = await db.Users.Where(user => user.AuthId == authId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(ct);
            if (userId is not null)
            {
                await db.Sessions.Where(session => session.UserId == userId && !session.Revoked).ExecuteUpdateAsync(update => update.SetProperty(session => session.Revoked, true), ct);
                db.Audit.Add(new AuditEvent { ActorId = userId, At = clock.GetUtcNow(), Operation = "RESTABLECIMIENTO_CONTRASENA", Entity = userId.Value.ToString(), Outcome = "SESIONES_REVOCADAS", Correlation = http.TraceIdentifier });
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().Document<object>("RestablecerContrasena", "RS-09 / RS-11", 204).Validated<ResetRequest>(v =>
        {
            v.RuleFor(x => x.Token).NotEmpty().MaximumLength(8192);
            Password(v.RuleFor(x => x.NuevaContrasena));
        });
    }
}

public sealed record ResetRequest(string Token, string NuevaContrasena);
