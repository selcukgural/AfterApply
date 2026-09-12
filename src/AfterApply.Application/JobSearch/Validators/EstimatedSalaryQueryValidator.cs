using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class EstimatedSalaryQueryValidator : AbstractValidator<EstimatedSalaryQuery>
{
    public EstimatedSalaryQueryValidator()
    {
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(JobSearchValidationRules.MaxTitleLength);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(JobSearchValidationRules.MaxTitleLength);
        RuleFor(x => x.LocationType).IsInEnum();
        RuleFor(x => x.YearsOfExperience).IsInEnum();
    }
}
