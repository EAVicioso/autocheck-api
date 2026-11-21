using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Usuarios.ActualizarPerfil;

public sealed class Validator : AbstractValidator<ProfileRequest>
{
    public Validator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Celular).NotNull().MaximumLength(30);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
