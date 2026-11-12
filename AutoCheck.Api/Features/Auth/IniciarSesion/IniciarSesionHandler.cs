// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.


namespace AutoCheck.Api.Features.Auth;

public sealed record LoginRequest(string Correo, string Contrasena);

public static partial class AuthEndpoints
{
    private static async Task<IResult> Login(LoginRequest r, HttpContext h, AutoCheckDb db, IIdentityProvider provider, TimeProvider clock, CancellationToken ct)
    {
        var email = ApiContract.NormalizeEmail(r.Correo);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("account:" + email, ct);
        var counter = await db.AccountLocks.FindAsync([email], ct);
        if (counter is null) { counter = new() { Email = email }; db.AccountLocks.Add(counter); }
        var now = clock.GetUtcNow();
        if (counter.LockedUntil > now) throw new ApiException(429, "CUENTA_BLOQUEADA", "La cuenta está bloqueada temporalmente.");
        if (counter.LockedUntil is not null)
        {
            counter.Failures = 0; counter.LockedUntil = null;
            db.Audit.Add(new() { At = now, Operation = "FIN_BLOQUEO", Entity = ApiContract.Hash(email), Outcome = "DESBLOQUEADO", Correlation = h.TraceIdentifier });
        }
        var session = await provider.Login(email, r.Contrasena, ct);
        now = clock.GetUtcNow();
        if (session is null)
        {
            counter.Failures++;
            if (counter.Failures >= 5)
            {
                counter.LockedUntil = now.AddMinutes(15);
                db.Audit.Add(new() { At = now, Operation = "INICIO_BLOQUEO", Entity = ApiContract.Hash(email), Outcome = "BLOQUEADO", Correlation = h.TraceIdentifier });
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw new ApiException(401, "CREDENCIALES_INVALIDAS", "Credenciales inválidas.");
        }
        var user = await db.Users.SingleOrDefaultAsync(x => x.AuthId == session.AuthId, ct);
        if (user is null)
        {
            // Reconciles a provider registration whose PostgreSQL confirmation was interrupted.
            user = new() { AuthId = session.AuthId, Email = email };
            db.Users.Add(user);
        }
        if (!user.Active) AppActor.Deny();
        counter.Failures = 0;
        db.Sessions.Add(new() { Id = session.SessionId, UserId = user.Id, LastActivity = now });
        db.Audit.Add(new() { ActorId = user.Id, At = now, Operation = "LOGIN", Entity = session.SessionId.ToString(), Outcome = "AUTORIZADO", Correlation = h.TraceIdentifier });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Ok(new SessionResponse(session.AccessToken, session.RefreshToken, now.AddSeconds(session.ExpiresIn)));
    }
}
