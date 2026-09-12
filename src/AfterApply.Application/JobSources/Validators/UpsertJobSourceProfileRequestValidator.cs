using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;
using FluentValidation;

namespace AfterApply.Application.JobSources.Validators;

public sealed class UpsertJobSourceProfileRequestValidator : AbstractValidator<UpsertJobSourceProfileRequest>
{
    public UpsertJobSourceProfileRequestValidator()
    {
        RuleFor(x => x.Titles).NotNull()
            .Must(t => t.Count is >= 1 and <= UserJobSourceProfile.MaxTitles)
            .WithMessage($"Between 1 and {UserJobSourceProfile.MaxTitles} titles.");
        RuleForEach(x => x.Titles).NotEmpty().Length(2, JobSourceQuery.MaxKeywordsLength).Must(BePlainText)
            .WithMessage("Control characters are not allowed.");
        RuleFor(x => x.Location).NotEmpty().Length(2, JobSourceQuery.MaxLocationLength).Must(BePlainText)
            .WithMessage("Control characters are not allowed.");
    }

    // The words are going into a URL we build; a stray control character is at best noise and at
    // worst a log-injection attempt, so it is refused rather than escaped.
    private static bool BePlainText(string? value) => value is null || !value.Any(char.IsControl);
}

public sealed class UpdateUserJobSourceLimitsRequestValidator : AbstractValidator<UpdateUserJobSourceLimitsRequest>
{
    public UpdateUserJobSourceLimitsRequestValidator()
    {
        RuleFor(x => x.WeeklyPostingLimit).InclusiveBetween(0, 500).When(x => x.WeeklyPostingLimit is not null);
    }
}

public sealed class GrantProEntitlementRequestValidator : AbstractValidator<GrantProEntitlementRequest>
{
    public GrantProEntitlementRequestValidator()
    {
        RuleFor(x => x.ActiveUntil).Must(until => until > DateTimeOffset.UtcNow).WithMessage("ActiveUntil must be in the future.");
    }
}
