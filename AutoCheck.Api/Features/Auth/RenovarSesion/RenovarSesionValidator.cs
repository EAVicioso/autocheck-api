// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

using Microsoft.AspNetCore.Http.HttpResults;
using AutoCheck.Api.Features.Auth.Common;
using static AutoCheck.Api.Features.Auth.Common.AuthSupport;
namespace AutoCheck.Api.Features.Auth.RenovarSesion;

public sealed class Validator : AbstractValidator<RefreshRequest>
{
    public Validator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(4096);
    }
}
