using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Application.Common;
using FluentValidation;

namespace AfterApply.Application.TrackedJobs.Validators;

public sealed class ResolveTrackedJobLinkRequestValidator : AbstractValidator<ResolveTrackedJobLinkRequest>
{
    public ResolveTrackedJobLinkRequestValidator()
    {
        RuleFor(x => x.JobUrl).NotEmpty().MaximumLength(2000).MustBeAWebUrl();
    }
}
