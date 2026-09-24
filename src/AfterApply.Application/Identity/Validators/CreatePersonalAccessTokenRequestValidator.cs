using AfterApply.Application.Identity.Contracts;
using FluentValidation;

namespace AfterApply.Application.Identity.Validators;

public sealed class CreatePersonalAccessTokenRequestValidator : AbstractValidator<CreatePersonalAccessTokenRequest>
{
    public CreatePersonalAccessTokenRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        // The extension is the only consumer, and a session-equivalent token outlives the session
        // that minted it by 90 days (2026-09-24). Full stays in the enum for rows issued before.
        RuleFor(x => x.Scope).IsInEnum().Equal(PersonalAccessTokenScope.Extension);
    }
}
