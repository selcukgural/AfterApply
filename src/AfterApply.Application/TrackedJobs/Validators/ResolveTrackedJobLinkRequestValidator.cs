using AfterApply.Application.TrackedJobs.Contracts;
using FluentValidation;

namespace AfterApply.Application.TrackedJobs.Validators;

public sealed class ResolveTrackedJobLinkRequestValidator : AbstractValidator<ResolveTrackedJobLinkRequest>
{
    public ResolveTrackedJobLinkRequestValidator()
    {
        RuleFor(x => x.JobUrl).NotEmpty().MaximumLength(2000);
    }
}
