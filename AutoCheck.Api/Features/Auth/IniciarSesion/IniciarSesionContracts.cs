// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.IniciarSesion;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest(string Correo, string Contrasena)
{
    public override string ToString() => "LoginRequest [REDACTADO]";
}

public sealed record AttemptSnapshot(int Failures, DateTimeOffset? LockedUntil);
public sealed record FailureState(int Failures, DateTimeOffset? LockedUntil);
public sealed record SecuritySnapshot(bool Active, DateTimeOffset? ArchivedAt, bool ResetPending, long SessionVersion, long InitialVersion);
