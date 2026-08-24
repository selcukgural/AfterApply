using AfterApply.Application.Applications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class UpdateApplicationRequestValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationRequestValidator()
    {
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.JobUrl).MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.AppliedAt).LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage("AppliedAt cannot be in the future.");
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
