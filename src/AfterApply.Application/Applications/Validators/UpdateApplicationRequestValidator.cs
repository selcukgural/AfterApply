using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

public sealed class UpdateApplicationRequestValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobUrl).MaximumLength(2000).MustBeAWebUrl();
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.AppliedAt).LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage(_ => localizer["VALIDATION_APPLIED_AT_FUTURE"]);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
