// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.Common;

public sealed record SessionResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiraUtc)
{
    public override string ToString() => "SessionResponse [REDACTADO]";
}
