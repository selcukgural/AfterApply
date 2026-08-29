using AfterApply.Application.Localization;
using AfterApply.Application.TrackedJobs.Contracts;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.TrackedJobs.Validators;

public sealed class ConvertTrackedJobRequestValidator : AbstractValidator<ConvertTrackedJobRequest>
{
    public ConvertTrackedJobRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.AppliedAt).LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage(_ => localizer["VALIDATION_APPLIED_AT_FUTURE"]);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
