// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.CerrarSesion;

public sealed record Command(string AccessToken)
{
    public override string ToString() => "Logout credentials [REDACTADO]";
}
