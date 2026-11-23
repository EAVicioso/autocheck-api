// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RegistrarCiudadano;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest(string Correo, string Contrasena, string Nombre)
{
    public override string ToString() => "RegisterRequest [REDACTADO]";
}

public sealed record RegistrationResponse(Guid UsuarioId, string Estado);
