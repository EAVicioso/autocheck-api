// AutoCheck / INTEC, SRS 1.1. Vertical slice; history: Git.


namespace AutoCheck.Api.Features.Auth;

public sealed record RegisterRequest(string Correo, string Contrasena, string Nombre);

public static partial class AuthEndpoints
{
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
}
