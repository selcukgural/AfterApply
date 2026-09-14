using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Identity.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
        // Optional since 2026-09-14: the sign-up form no longer asks for a name (the account works
        // without one and Settings can add it later). The column is NOT NULL, so the contract still
        // carries the strings — empty, not null — and the length cap still applies.
        RuleFor(x => x.FirstName).NotNull().MaximumLength(100);
        RuleFor(x => x.LastName).NotNull().MaximumLength(100);
        RuleFor(x => x.ConsentAccepted).Must(x => x).WithMessage(_ => localizer["VALIDATION_CONSENT_REQUIRED"]);
    }
}
