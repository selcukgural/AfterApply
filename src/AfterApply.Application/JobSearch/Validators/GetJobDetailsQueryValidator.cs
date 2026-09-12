using AfterApply.Application.JobSearch.Contracts;
using FluentValidation;

namespace AfterApply.Application.JobSearch.Validators;

public sealed class GetJobDetailsQueryValidator : AbstractValidator<GetJobDetailsQuery>
{
    public GetJobDetailsQueryValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
        // The provider's batch ceiling. The server's own, lower cap (JobSearch:MaxJobIdsPerDetails
        // or the user's override) is enforced by the service as a refusal, not a silent trim.
        RuleFor(x => x.Ids)
            .Must(v => JobSearchCsv.Split(v).Count is >= 1 and <= JobSearchValidationRules.UpstreamMaxJobIds)
            .WithMessage($"Between 1 and {JobSearchValidationRules.UpstreamMaxJobIds} job ids.")
            .Must(v => JobSearchCsv.Split(v).All(IsWellFormedId))
            .WithMessage($"Each job id must be at most {JobSearchValidationRules.MaxJobIdLength} characters with no whitespace.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ids));
        RuleFor(x => x.Country).MustBeCountryCode();
        RuleFor(x => x.Language).MustBeLanguageCode();
    }

    private static bool IsWellFormedId(string id) =>
        id.Length <= JobSearchValidationRules.MaxJobIdLength
        && id.All(c => !char.IsWhiteSpace(c) && !char.IsControl(c));
}
