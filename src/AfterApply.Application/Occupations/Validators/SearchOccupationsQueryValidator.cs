using AfterApply.Application.Occupations.Contracts;
using FluentValidation;

namespace AfterApply.Application.Occupations.Validators;

public sealed class SearchOccupationsQueryValidator : AbstractValidator<SearchOccupationsQuery>
{
    public SearchOccupationsQueryValidator()
    {
        // No minimum on purpose: a one-letter query is a valid "not enough to search yet" and gets
        // an empty list, not a 400 — the same reasoning as SearchCompaniesQueryValidator.
        RuleFor(x => x.Q).NotNull().MaximumLength(120);
    }
}
