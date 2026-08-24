using AfterApply.Application.Applications.Contracts;
using FluentValidation;

namespace AfterApply.Application.Applications.Validators;

public sealed class GetApplicationsQueryValidator : AbstractValidator<GetApplicationsQuery>
{
    public GetApplicationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDirection).IsInEnum();
    }
}
