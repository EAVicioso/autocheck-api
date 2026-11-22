// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RegistrarCiudadano;

public sealed class Validator : AbstractValidator<RegisterRequest>
{
    public Validator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
        Password(RuleFor(x => x.Contrasena));
    }
}
