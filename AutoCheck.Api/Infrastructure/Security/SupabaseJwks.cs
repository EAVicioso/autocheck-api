// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

// AutoCheck academic prototype. Supabase JWKS with automatic key rotation.
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AutoCheck.Api.Infrastructure.Security;

public sealed class SupabaseJwks(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    // IdentityModel owns the shared refresh, while the request can stop waiting for it.
    public static async Task WaitForConfiguration(MessageReceivedContext context)
    {
        const string prefix = "Bearer ";
        var authorization = context.Request.Headers.Authorization.ToString();
        if (context.Options.ConfigurationManager is not { } manager ||
            !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            authorization.AsSpan(prefix.Length).Trim().IsEmpty)
        {
            return;
        }

        var requestAborted = context.HttpContext.RequestAborted;
        requestAborted.ThrowIfCancellationRequested();
        await manager.GetConfigurationAsync(requestAborted).WaitAsync(requestAborted);
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var json = await retriever.GetDocumentAsync(address, timeout.Token);
        var result = new OpenIdConnectConfiguration
        {
            Issuer = issuer
        };
        foreach (var key in new JsonWebKeySet(json).GetSigningKeys())
        {
            result.SigningKeys.Add(key);
        }

        return result;
    }
}
