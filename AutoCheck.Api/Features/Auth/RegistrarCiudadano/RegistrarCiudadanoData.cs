// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RegistrarCiudadano;

public sealed class Data(AutoCheckDb db, TimeProvider clock) : IData
{
    public async Task<RegistrationResponse> Execute(RegisterRequest request, HttpContext http, Func<CancellationToken, Task<ProviderRegistration>> createIdentity, CancellationToken ct)
    {
        var email = ApiContract.NormalizeEmail(request.Correo);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync("account:" + email, ct);
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
        {
            throw ApiContract.Conflict(ApiErrorCode.CorreoExistente, "El correo ya está registrado.");
        }

        var registered = await createIdentity(ct);
        var account = new UserAccount
        {
            AuthId = registered.AuthId,
            Email = email,
            Name = request.Nombre.Trim()
        };
        db.Users.Add(account);
        db.Audit.Add(new() { ActorId = account.Id, At = clock.GetUtcNow(), Operation = "REGISTRO", Entity = account.Id.ToString(), Outcome = "CREADO", Correlation = http.TraceIdentifier });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RegistrationResponse(account.Id, registered.NeedsConfirmation ? "PENDIENTE_CONFIRMACION" : "ACTIVA");
    }
}
