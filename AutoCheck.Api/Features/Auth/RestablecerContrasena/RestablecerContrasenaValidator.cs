// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public sealed class Validator : AbstractValidator<ResetRequest>
{
    public Validator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(8192);
        Password(RuleFor(x => x.NuevaContrasena));
    }
}
