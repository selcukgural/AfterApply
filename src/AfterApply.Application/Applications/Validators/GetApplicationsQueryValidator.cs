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
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty).When(x => x.CompanyId.HasValue);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDirection).IsInEnum();
    }
}

public sealed class GetGroupedApplicationsQueryValidator : AbstractValidator<GetGroupedApplicationsQuery>
{
    public GetGroupedApplicationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // Companies, not applications — ten companies can already be a long page once their
        // applications are drawn under them, so the ceiling is lower than the flat list's.
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDirection).IsInEnum();
    }
}
