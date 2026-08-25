using AfterApply.Application.Matching.Contracts;
using FluentValidation;

namespace AfterApply.Application.Matching.Validators;

public sealed class ComputeJobMatchRequestValidator : AbstractValidator<ComputeJobMatchRequest>
{
    public ComputeJobMatchRequestValidator()
    {
        RuleFor(x => x.JobDescription).NotEmpty().MaximumLength(20_000);
    }
}
