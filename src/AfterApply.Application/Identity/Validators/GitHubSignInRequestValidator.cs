using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class GitHubSignInRequestValidator : AbstractValidator<GitHubSignInRequest>
{
    public GitHubSignInRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.RedirectUri).NotEmpty().MaximumLength(2048)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                         && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps));
    }
}
