using Microsoft.AspNetCore.Authentication.JwtBearer;
namespace AutoCheck.Api.Infrastructure.Security;

public static class TransportClaims
{
    public static string[] Algorithms(bool testing) => testing ? [Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256] : [Microsoft.IdentityModel.Tokens.SecurityAlgorithms.EcdsaSha256];
    public static Task Validate(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst("sub")?.Value, out _) || !Guid.TryParse(principal?.FindFirst("session_id")?.Value, out _) || principal?.FindFirst("role")?.Value != "authenticated" || principal?.FindFirst("is_anonymous")?.Value == "true")
        {
            context.Fail("Invalid transport claims.");
        }
        return Task.CompletedTask;
    }
}
