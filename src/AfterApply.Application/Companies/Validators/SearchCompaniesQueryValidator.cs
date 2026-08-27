using AfterApply.Application.Companies.Contracts;
using FluentValidation;

namespace AfterApply.Application.Companies.Validators;

public sealed class SearchCompaniesQueryValidator : AbstractValidator<SearchCompaniesQuery>
{
    public SearchCompaniesQueryValidator()
    {
        // No MinimumLength rule: a short query is a valid "not enough to search yet" case
        // (the service returns an empty list for it), not a validation error.
        RuleFor(x => x.Q).NotNull().MaximumLength(300);
    }
}
