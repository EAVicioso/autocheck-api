// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.SolicitarRecuperacion;

public sealed class Validator : AbstractValidator<RecoveryRequest>
{
    public Validator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254);
    }
}
