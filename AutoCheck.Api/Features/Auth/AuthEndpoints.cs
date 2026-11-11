// AutoCheck academic prototype, SRS 1.1; CU-01/02, PA-01/02, RS-09/10/11.
namespace AutoCheck.Api.Features.Auth;

public sealed record RegisterRequest(string Correo, string Contrasena, string Nombre);
public sealed record LoginRequest(string Correo, string Contrasena);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RecoveryRequest(string Correo);
public sealed record ResetRequest(string Token, string NuevaContrasena);
public sealed record SessionResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiraUtc);
public sealed record RegistrationResponse(Guid UsuarioId, string Estado);

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/auth").WithTags("Identidad").RequireRateLimiting("auth");
        g.MapPost("/registro", Register).AllowAnonymous().Document<RegistrationResponse>("RegistrarCiudadano", "CU-01 / PA-01 / RS-09", 201)
            .Validated<RegisterRequest>(v => { v.RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254); v.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120); Password(v.RuleFor(x => x.Contrasena)); });
        g.MapPost("/login", Login).AllowAnonymous().Document<SessionResponse>("IniciarSesion", "CU-02 / PA-02 / RS-11")
            .Validated<LoginRequest>(v => { v.RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254); v.RuleFor(x => x.Contrasena).NotEmpty().MaximumLength(256); });
        g.MapPost("/refresh", Refresh).AllowAnonymous().Document<SessionResponse>("RenovarSesion", "CU-02 / RS-10")
            .Validated<RefreshRequest>(v => v.RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(4096));
        g.MapPost("/recuperacion", async (RecoveryRequest r, IIdentityProvider provider, CancellationToken ct) =>
        {
            await provider.Recover(ApiContract.NormalizeEmail(r.Correo), ct);
            return Results.Accepted(value: new { mensaje = "Si la cuenta existe, recibirás instrucciones de recuperación." });
        }).AllowAnonymous().Document<object>("SolicitarRecuperacion", "IS-04 / RS-11", 202)
            .Validated<RecoveryRequest>(v => v.RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254));
        g.MapPost("/restablecimiento", async (ResetRequest r, IIdentityProvider provider, CancellationToken ct) =>
        {
            await provider.Reset(r.Token, r.NuevaContrasena, ct);
            return Results.NoContent();
        }).AllowAnonymous().Document<object>("RestablecerContrasena", "RS-09 / RS-11", 204)
            .Validated<ResetRequest>(v => { v.RuleFor(x => x.Token).NotEmpty().MaximumLength(8192); Password(v.RuleFor(x => x.NuevaContrasena)); });
        g.MapPost("/logout", async (HttpContext h, AppActor actor, AutoCheckDb db, CancellationToken ct) =>
        {
            // Local revocation is immediate even while the identity provider is unavailable.
            await db.Sessions.Where(x => x.Id == actor.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
            return Results.NoContent();
        }).RequireAuthorization().Document<object>("CerrarSesion", "CU-02 / RS-10", 204);
        g.MapPost("/actividad", () => Results.NoContent()).RequireAuthorization().Interactive()
            .Document<object>("RegistrarActividad", "RS-10: acción explícita del usuario, nunca polling", 204);
    }

    private static void Password<T>(FluentValidation.IRuleBuilderInitial<T, string> rule) =>
        rule.NotEmpty().MinimumLength(8).MaximumLength(256).Matches("[a-zA-Z]").Matches("[0-9]");

    private static async Task<IResult> Register(RegisterRequest r, HttpContext h, AutoCheckDb db, IIdentityProvider provider, TimeProvider clock, CancellationToken ct)
    {
        var email = ApiContract.NormalizeEmail(r.Correo);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("account:" + email, ct);
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw ApiContract.Conflict("CORREO_EXISTENTE", "El correo ya está registrado.");
        var registered = await provider.Register(email, r.Contrasena, ct);
        var account = new UserAccount { AuthId = registered.AuthId, Email = email, Name = r.Nombre.Trim() };
        db.Users.Add(account);
        db.Audit.Add(new() { ActorId = account.Id, At = clock.GetUtcNow(), Operation = "REGISTRO", Entity = account.Id.ToString(), Outcome = "CREADO", Correlation = h.TraceIdentifier });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Created("/api/usuarios/me", new RegistrationResponse(account.Id, registered.NeedsConfirmation ? "PENDIENTE_CONFIRMACION" : "ACTIVA"));
    }

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

    private static async Task<IResult> Refresh(RefreshRequest r, AutoCheckDb db, IIdentityProvider provider, TimeProvider clock, CancellationToken ct)
    {
        var tokens = await provider.Refresh(r.RefreshToken, ct) ?? throw new ApiException(401, "SESION_INVALIDA", "Sesión inválida.");
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tokens.SessionId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.AuthId == tokens.AuthId, ct);
        if (session is null || user is null || session.UserId != user.Id || session.Revoked || !user.Active || clock.GetUtcNow() - session.LastActivity >= TimeSpan.FromHours(1))
            throw new ApiException(401, "SESION_EXPIRADA", "La sesión expiró.");
        return Results.Ok(new SessionResponse(tokens.AccessToken, tokens.RefreshToken, clock.GetUtcNow().AddSeconds(tokens.ExpiresIn)));
    }
}
