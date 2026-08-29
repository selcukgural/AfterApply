using AfterApply.Application.TrackedJobs.Contracts;
using FluentValidation;

namespace AfterApply.Application.TrackedJobs.Validators;

public sealed class CreateTrackedJobRequestValidator : AbstractValidator<CreateTrackedJobRequest>
{
    public CreateTrackedJobRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobUrl).MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
