// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck academic prototype, SRS 1.1; IS-01, RD-11.
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AutoCheck.Api.Infrastructure.Security;

public sealed record ProviderSession(Guid AuthId, Guid SessionId, string AccessToken, string RefreshToken, int ExpiresIn)
{
    public override string ToString() => "ProviderSession [REDACTADO]";
}
public sealed record ProviderRecovery(Guid AuthId, string AccessToken)
{
    public override string ToString() => "ProviderRecovery [REDACTADO]";
}
public sealed record ProviderRegistration(Guid AuthId, bool NeedsConfirmation);
public interface IIdentityProvider
{
    Task<ProviderRegistration> Register(string email, string password, CancellationToken ct);
    Task<ProviderSession?> Login(string email, string password, CancellationToken ct);
    Task<ProviderSession?> Refresh(string refreshToken, CancellationToken ct);
    Task Recover(string email, CancellationToken ct);
    Task<ProviderRecovery> ValidateRecovery(string tokenHash, CancellationToken ct);
    Task<Guid> Reset(string recoveryToken, string password, CancellationToken ct);
    Task Logout(string accessToken, CancellationToken ct);
}

public sealed class SupabaseIdentity(HttpClient http, IConfiguration configuration) : IIdentityProvider
{
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken ct, string? token = null)
    {
        var url = configuration["Supabase:Url"];
        var key = configuration["Supabase:PublishableKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw ApiContract.Unavailable();
        }

        using var request = new HttpRequestMessage(method, url.TrimEnd('/') + "/auth/v1/" + path);
        request.Headers.Add("apikey", key);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(request, ct);
    }

    public async Task<ProviderRegistration> Register(string email, string password, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "signup", new { email, password }, ct);
        EnsureAvailable(response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            throw ApiContract.Invalid("No se pudo registrar la cuenta. Revisa el correo y la contraseña.");
        }

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        var user = root.TryGetProperty("user", out var nested) ? nested : root;
        if (user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("identities", out var identities)
            || identities.ValueKind != JsonValueKind.Array
            || !user.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
            || !id.TryGetGuid(out var authId))
        {
            throw ApiContract.Unavailable();
        }

        if (identities.GetArrayLength() == 0)
        {
            throw ApiContract.Conflict(ApiErrorCode.CorreoExistente, "El correo ya está registrado.");
        }

        var needsConfirmation = !root.TryGetProperty("access_token", out var access)
            || access.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(access.GetString());
        return new(authId, needsConfirmation);
    }

    public Task<ProviderSession?> Login(string email, string password, CancellationToken ct) => Tokens("password", new { email, password }, ct);
    public Task<ProviderSession?> Refresh(string refreshToken, CancellationToken ct) => Tokens("refresh_token", new { refresh_token = refreshToken }, ct);
    private async Task<ProviderSession?> Tokens(string grant, object body, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "token?grant_type=" + grant, body, ct);
        EnsureAvailable(response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        var access = root.GetProperty("access_token").GetString()!;
        // This token arrived directly from the trusted HTTPS provider, never from a client assertion.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        return new(Guid.Parse(jwt.Subject), Guid.Parse(jwt.Claims.Single(x => x.Type == "session_id").Value), access, root.GetProperty("refresh_token").GetString()!, root.GetProperty("expires_in").GetInt32());
    }

    public async Task Recover(string email, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "recover", new { email }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw ApiContract.Unavailable();
        }
    }

    public async Task<Guid> Reset(string recoveryToken, string password, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Put, "user", new { password }, ct, recoveryToken);
        if ((int)response.StatusCode is 400 or 401 or 403 or 404 or 422)
        {
            throw new RecoveryRejectedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw ApiContract.Unavailable();
        }

        // Identity comes from the authenticated provider response, never decoded
        // client JWT claims or a user-supplied id.
        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return result.RootElement.GetProperty("id").GetGuid();
    }

    public async Task<ProviderRecovery> ValidateRecovery(string tokenHash, CancellationToken ct)
    {
        // Auth consumes the email proof before a password-changing operation can begin.
        using var response = await Send(HttpMethod.Post, "verify", new { token_hash = tokenHash, type = "recovery" }, ct);
        if ((int)response.StatusCode is 400 or 401 or 403 or 404 or 422)
        {
            throw new ApiException(401, "RECUPERACION_INVALIDA", "El enlace de recuperación no es válido.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw ApiContract.Unavailable();
        }
        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = result.RootElement;
        if (!root.TryGetProperty("user", out var user) || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
            || !id.TryGetGuid(out var authId)
            || !root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(token.GetString()))
        {
            throw ApiContract.Unavailable();
        }

        return new(authId, token.GetString()!);
    }

    public async Task Logout(string accessToken, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "logout?scope=local", null, ct, accessToken);
        if (!response.IsSuccessStatusCode && (int)response.StatusCode is not (401 or 403 or 404))
        {
            throw ApiContract.Unavailable();
        }
    }

    private static void EnsureAvailable(HttpStatusCode status)
    {
        if ((int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
        {
            throw ApiContract.Unavailable();
        }
    }
}

public sealed class RecoveryRejectedException() : Exception("Auth rejected the password reset without applying it.");
