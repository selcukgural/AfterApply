using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class UpdateJobSearchLimitsRequestValidator : AbstractValidator<UpdateJobSearchLimitsRequest>
{
    /// <summary>An admin may give one account at most a BASIC month's worth of credits per day —
    /// beyond that the override would be the quota, not a share of it.</summary>
    public const int MaxDailyCredits = 200;

    public UpdateJobSearchLimitsRequestValidator()
    {
        RuleFor(x => x.PerUserDailyCredits).InclusiveBetween(0, MaxDailyCredits).When(x => x.PerUserDailyCredits is not null);
        RuleFor(x => x.MaxPagesPerSearch).InclusiveBetween(1, JobSearchValidationRules.UpstreamMaxPages)
            .When(x => x.MaxPagesPerSearch is not null);
        RuleFor(x => x.MaxJobIdsPerDetails).InclusiveBetween(1, JobSearchValidationRules.UpstreamMaxJobIds)
            .When(x => x.MaxJobIdsPerDetails is not null);
    }
}
