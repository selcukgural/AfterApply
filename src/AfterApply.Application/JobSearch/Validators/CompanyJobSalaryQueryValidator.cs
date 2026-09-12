using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class CompanyJobSalaryQueryValidator : AbstractValidator<CompanyJobSalaryQuery>
{
    public CompanyJobSalaryQueryValidator()
    {
        RuleFor(x => x.Company).NotEmpty().MaximumLength(JobSearchValidationRules.MaxTitleLength);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(JobSearchValidationRules.MaxTitleLength);
        RuleFor(x => x.Location).MaximumLength(JobSearchValidationRules.MaxTitleLength);
        RuleFor(x => x.LocationType).IsInEnum();
        RuleFor(x => x.YearsOfExperience).IsInEnum();
    }
}
