using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.IniciarSesion;

public sealed class Validator : AbstractValidator<LoginRequest>
{
    public Validator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Contrasena).NotEmpty().MaximumLength(256);
    }
}
