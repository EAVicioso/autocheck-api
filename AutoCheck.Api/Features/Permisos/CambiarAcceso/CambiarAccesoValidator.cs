using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Usuarios.Common;
using static AutoCheck.Api.Features.Usuarios.Common.UserSupport;
namespace AutoCheck.Api.Features.Permisos.CambiarAcceso;

public sealed class Validator : AbstractValidator<AccessRequest>
{
    public Validator()
    {
        RuleFor(x => x.Rol).Must(x => new[] { Roles.Citizen, Roles.Inspector, Roles.Admin }.Contains(x));
        RuleFor(x => x.Permisos).Must(x => !x.IsDefault);
        RuleForEach(x => x.Permisos).Must(Permissions.All.Contains);
        RuleFor(x => x.Version).GreaterThan(0);
    }
}
