// AutoCheck academic prototype, SRS 1.1; IS-01, RD-11.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AutoCheck.Api.Infrastructure.Security;

public sealed record ProviderSession(Guid AuthId, Guid SessionId, string AccessToken, string RefreshToken, int ExpiresIn);
public sealed record ProviderRegistration(Guid AuthId, bool NeedsConfirmation);
public interface IIdentityProvider
{
    Task<ProviderRegistration> Register(string email, string password, CancellationToken ct);
    Task<ProviderSession?> Login(string email, string password, CancellationToken ct);
    Task<ProviderSession?> Refresh(string refreshToken, CancellationToken ct);
    Task Recover(string email, CancellationToken ct);
    Task Reset(string recoveryToken, string password, CancellationToken ct);
    Task Logout(string accessToken, CancellationToken ct);
}

public sealed class SupabaseIdentity(HttpClient http, IConfiguration configuration) : IIdentityProvider
{
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken ct, string? token = null)
    {
        var url = configuration["Supabase:Url"];
        var key = configuration["Supabase:PublishableKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) throw ApiContract.Unavailable();
        using var request = new HttpRequestMessage(method, url.TrimEnd('/') + "/auth/v1/" + path);
        request.Headers.Add("apikey", key);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request, ct);
    }

    public async Task<ProviderRegistration> Register(string email, string password, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "signup", new { email, password }, ct);
        if (!response.IsSuccessStatusCode) throw ApiContract.Invalid("No se pudo registrar la cuenta. Revisa el correo y la contraseña.");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        var user = root.TryGetProperty("user", out var nested) ? nested : root;
        return new(user.GetProperty("id").GetGuid(), !root.TryGetProperty("access_token", out _));
    }

    public Task<ProviderSession?> Login(string email, string password, CancellationToken ct) => Tokens("password", new { email, password }, ct);
    public Task<ProviderSession?> Refresh(string refreshToken, CancellationToken ct) => Tokens("refresh_token", new { refresh_token = refreshToken }, ct);
    private async Task<ProviderSession?> Tokens(string grant, object body, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "token?grant_type=" + grant, body, ct);
        if ((int)response.StatusCode >= 500 || (int)response.StatusCode == 429) throw ApiContract.Unavailable();
        if (!response.IsSuccessStatusCode) return null;
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        var access = root.GetProperty("access_token").GetString()!;
        // This token arrived directly from the trusted HTTPS provider, never from a client assertion.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        return new(Guid.Parse(jwt.Subject), Guid.Parse(jwt.Claims.Single(x => x.Type == "session_id").Value), access,
            root.GetProperty("refresh_token").GetString()!, root.GetProperty("expires_in").GetInt32());
    }

    public async Task Recover(string email, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "recover", new { email }, ct);
        if ((int)response.StatusCode >= 500) throw ApiContract.Unavailable();
    }
    public async Task Reset(string recoveryToken, string password, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Put, "user", new { password }, ct, recoveryToken);
        if (!response.IsSuccessStatusCode) throw new ApiException(401, "RECUPERACION_INVALIDA", "El enlace de recuperación no es válido.");
    }
    public async Task Logout(string accessToken, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Post, "logout?scope=local", null, ct, accessToken);
        if ((int)response.StatusCode >= 500) throw ApiContract.Unavailable();
    }
}
