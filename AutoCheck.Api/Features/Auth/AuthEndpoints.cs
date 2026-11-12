// AutoCheck academic prototype, SRS 1.1; CU-01/02, PA-01/02, RS-09/10/11.
namespace AutoCheck.Api.Features.Auth;

public sealed record RecoveryRequest(string Correo);
public sealed record ResetRequest(string Token, string NuevaContrasena);
public sealed record SessionResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiraUtc);
public sealed record RegistrationResponse(Guid UsuarioId, string Estado);

public static partial class AuthEndpoints
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
        rule.NotEmpty().MinimumLength(8).MaximumLength(256).Matches(@"\p{L}").Matches(@"\p{N}");


}
