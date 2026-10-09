using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.Applications.Validators;

public sealed class SetInterviewRequestValidator : AbstractValidator<SetInterviewRequest>
{
    public SetInterviewRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.InterviewAt).MustBeAReasonableInterviewDate(localizer);
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.With).MaximumLength(Domain.Applications.Application.InterviewWithMaxLength);
    }
}
