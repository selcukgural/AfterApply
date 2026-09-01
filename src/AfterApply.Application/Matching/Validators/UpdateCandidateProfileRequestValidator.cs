using AfterApply.Application.Localization;
using AfterApply.Application.Matching.Contracts;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Matching.Validators;

public sealed class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.CvText).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.OpenAiConsentAccepted).Must(x => x)
            .WithMessage(_ => localizer["VALIDATION_MATCHING_CONSENT_REQUIRED"]);
    }
}
