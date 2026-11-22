// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.IniciarSesion;

public sealed class Data(AutoCheckDb db, TimeProvider clock) : IData
{
    public async Task<SessionResponse> Execute(LoginRequest request, HttpContext http, Func<AttemptSnapshot, CancellationToken, Task<ProviderSession?>> authenticate, Func<int, DateTimeOffset, FailureState> failure, Action<SecuritySnapshot> authorize, CancellationToken ct)
    {
        var email = ApiContract.NormalizeEmail(request.Correo);
        var securityVersion = await db.Users.AsNoTracking().Where(x => x.Email == email).Select(x => (long?)x.SessionVersion).SingleOrDefaultAsync(ct) ?? 0;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("account:" + email, ct);
        var counter = await db.AccountLocks.FindAsync([email], ct);
        if (counter is null)
        {
            counter = new()
            {
                Email = email
            };
            db.AccountLocks.Add(counter);
        }

        var now = clock.GetUtcNow();
        if (counter.LockedUntil is not null && counter.LockedUntil <= now)
        {
            counter.Failures = 0;
            counter.LockedUntil = null;
            db.Audit.Add(new() { At = now, Operation = "FIN_BLOQUEO", Entity = ApiContract.Hash(email), Outcome = "DESBLOQUEADO", Correlation = http.TraceIdentifier });
        }

        var session = await authenticate(new AttemptSnapshot(counter.Failures, counter.LockedUntil), ct);
        now = clock.GetUtcNow();
        if (session is null)
        {
            var next = failure(counter.Failures, now);
            counter.Failures = next.Failures;
            counter.LockedUntil = next.LockedUntil;
            if (next.LockedUntil is not null)
            {
                db.Audit.Add(new() { At = now, Operation = "INICIO_BLOQUEO", Entity = ApiContract.Hash(email), Outcome = "BLOQUEADO", Correlation = http.TraceIdentifier });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new ApiException(401, ApiErrorCode.CredencialesInvalidas, "Credenciales inválidas.");
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.AuthId == session.AuthId, ct);
        if (user is null)
        {
            // Reconciles a provider registration whose PostgreSQL confirmation was interrupted.
            user = new()
            {
                AuthId = session.AuthId,
                Email = email
            };
            db.Users.Add(user);
        }

        await db.LockAsync("session-security:" + session.AuthId, ct);
        if (db.Entry(user).State != EntityState.Added)
        {
            await db.Entry(user).ReloadAsync(ct);
        }
        authorize(new SecuritySnapshot(user.Active, user.ArchivedAt, user.PasswordResetPending, user.SessionVersion, securityVersion));

        counter.Failures = 0;
        db.Sessions.Add(new() { Id = session.SessionId, UserId = user.Id, LastActivity = now, SecurityVersion = user.SessionVersion });
        db.Audit.Add(new() { ActorId = user.Id, At = now, Operation = "LOGIN", Entity = session.SessionId.ToString(), Outcome = "AUTORIZADO", Correlation = http.TraceIdentifier });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new SessionResponse(session.AccessToken, session.RefreshToken, now.AddSeconds(session.ExpiresIn));
    }
}
