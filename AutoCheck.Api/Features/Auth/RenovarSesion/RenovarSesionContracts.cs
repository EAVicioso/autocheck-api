using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RenovarSesion;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record RefreshRequest(string RefreshToken)
{
    public override string ToString() => "RefreshRequest [REDACTADO]";
}

public sealed record Snapshot(bool Matches, bool Revoked, bool Active, DateTimeOffset? ArchivedAt, bool ResetPending, long SecurityVersion, long SessionVersion, DateTimeOffset LastActivity);
