using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class SearchJobsQueryValidator : AbstractValidator<SearchJobsQuery>
{
    public SearchJobsQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(JobSearchValidationRules.MaxQueryLength);
        RuleFor(x => x.Cursor).MaximumLength(JobSearchValidationRules.MaxCursorLength);
        // The provider's own range. The server's tighter cap (JobSearch:MaxPagesPerSearch, or the
        // user's override) is applied as a clamp by the service, which is where the options live.
        RuleFor(x => x.NumPages).InclusiveBetween(1, JobSearchValidationRules.UpstreamMaxPages)
            .When(x => x.NumPages is not null);
        RuleFor(x => x.Country).MustBeCountryCode();
        RuleFor(x => x.Language).MustBeLanguageCode();
        RuleFor(x => x.Location).MaximumLength(JobSearchValidationRules.MaxLocationLength);
        RuleFor(x => x.DatePosted).IsInEnum().When(x => x.DatePosted is not null);
        RuleFor(x => x.EmploymentTypes).MustBeEnumList<SearchJobsQuery, JobSearchEmploymentType>();
        RuleFor(x => x.JobRequirements).MustBeEnumList<SearchJobsQuery, JobSearchJobRequirement>();
        RuleFor(x => x.Radius).InclusiveBetween(1, JobSearchValidationRules.MaxRadiusKm).When(x => x.Radius is not null);
        RuleFor(x => x.ExcludeJobPublishers)
            .MaximumLength(JobSearchValidationRules.MaxPublisherItems * (JobSearchValidationRules.MaxPublisherLength + 1))
            .Must(v => JobSearchCsv.Split(v).Count <= JobSearchValidationRules.MaxPublisherItems)
            .WithMessage($"At most {JobSearchValidationRules.MaxPublisherItems} publishers.")
            .Must(v => JobSearchCsv.Split(v).All(p => p.Length <= JobSearchValidationRules.MaxPublisherLength))
            .WithMessage($"Each publisher name must be at most {JobSearchValidationRules.MaxPublisherLength} characters.");
    }
}
