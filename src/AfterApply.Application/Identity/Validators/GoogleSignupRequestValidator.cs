using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Identity.Validators;

/// <summary>Same name/consent rules as <see cref="RegisterRequestValidator"/> — a Google sign-up is
/// a sign-up, the only thing it skips is the password.</summary>
public sealed class GoogleSignupRequestValidator : AbstractValidator<GoogleSignupRequest>
{
    public GoogleSignupRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.SignupToken).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ConsentAccepted).Must(x => x).WithMessage(_ => localizer["VALIDATION_CONSENT_REQUIRED"]);
    }
}
