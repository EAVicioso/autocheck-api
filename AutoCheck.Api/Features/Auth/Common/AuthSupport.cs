using AutoCheck.Api.Features.Auth;
using AutoCheck.Api.Features.Auth.Common;

namespace AutoCheck.Api.Features.Auth.Common;

public static class AuthSupport
{
    public static void Password<T>(FluentValidation.IRuleBuilderInitial<T, string> rule) => rule.NotEmpty().MinimumLength(8).MaximumLength(256).Matches(@"\p{L}").Matches(@"\p{N}");
}
