using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Identity.Validators;

/// <summary>Same name/consent rules as <see cref="GoogleSignupRequestValidator"/>. <see cref="LinkedInSignupRequest.Email"/>
/// is optional here — whether it is actually required depends on whether the signup token's identity
/// carried one of its own, which only <c>AuthService.CompleteLinkedInSignupAsync</c> knows (it
/// decodes the token); this validator only enforces that a supplied value is well-formed.</summary>
public sealed class LinkedInSignupRequestValidator : AbstractValidator<LinkedInSignupRequest>
{
    public LinkedInSignupRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.SignupToken).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.ConsentAccepted).Must(x => x).WithMessage(_ => localizer["VALIDATION_CONSENT_REQUIRED"]);
    }
}
