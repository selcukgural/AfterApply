using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.Localization;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.CandidateExperiences.Validators;

/// <summary>Field by field so the ProblemDetails names the field the form has to highlight. The
/// domain's <see cref="CandidateExperienceContent.Validate"/> repeats the rules at the boundary
/// that stores the row; this layer is the one that speaks the user's language. The statement
/// messages are the review form's — same rule, same wording.</summary>
public sealed class CandidateExperienceRequestValidator : AbstractValidator<CandidateExperienceRequest>
{
    public CandidateExperienceRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        RuleFor(x => x.OverallRating).InclusiveBetween(CandidateExperience.MinRating, CandidateExperience.MaxRating);

        RuleFor(x => x.CategoryRatings)
            .Must(list => list is null || list.Select(r => r.Category).Distinct().Count() == list.Count)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_CATEGORY_DUPLICATE"]);
        RuleForEach(x => x.CategoryRatings)
            .ChildRules(rating =>
            {
                // Overall is its own field, never one of these.
                rating.RuleFor(r => r.Category).IsInEnum().NotEqual(ExperienceCategory.Overall);
                rating.RuleFor(r => r.Rating).InclusiveBetween(CandidateExperience.MinRating, CandidateExperience.MaxRating);
            })
            .OverridePropertyName(nameof(CandidateExperienceRequest.CategoryRatings));

        Picks(localizer, x => x.LikedStatements, ReviewStatementKind.Liked, nameof(CandidateExperienceRequest.LikedStatements));
        Picks(localizer, x => x.ImprovableStatements, ReviewStatementKind.Improve, nameof(CandidateExperienceRequest.ImprovableStatements));

        RuleFor(x => x.Outcome!.Value).IsInEnum().When(x => x.Outcome.HasValue)
            .OverridePropertyName(nameof(CandidateExperienceRequest.Outcome));
        RuleFor(x => x.Duration!.Value).IsInEnum().When(x => x.Duration.HasValue)
            .OverridePropertyName(nameof(CandidateExperienceRequest.Duration));
        RuleFor(x => x.Stages!.Value).IsInEnum().When(x => x.Stages.HasValue)
            .OverridePropertyName(nameof(CandidateExperienceRequest.Stages));

        RuleFor(x => x.InterviewTypes)
            .Must(list => list is null || list.Distinct().Count() == list.Count)
            .WithMessage(_ => localizer["VALIDATION_EXPERIENCE_INTERVIEW_TYPES_DUPLICATE"]);
        RuleForEach(x => x.InterviewTypes).IsInEnum()
            .OverridePropertyName(nameof(CandidateExperienceRequest.InterviewTypes));
    }

    private void Picks(IStringLocalizer<SharedStrings> localizer,
        Func<CandidateExperienceRequest, IReadOnlyList<string>?> picks, ReviewStatementKind kind, string name)
    {
        RuleFor(x => picks(x))
            .Must(list => list is null || list.Count <= ExperienceStatementCatalogue.MaxPicksPerKind)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENTS_TOO_MANY", ExperienceStatementCatalogue.MaxPicksPerKind])
            .Must(list => list is null || list.Distinct(StringComparer.Ordinal).Count() == list.Count)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENTS_DUPLICATE"])
            .OverridePropertyName(name);
        // A key must exist and be of the list's kind: a "liked" sentence in the "improve" list
        // would publish the opposite of what the author meant.
        RuleForEach(x => picks(x))
            .Must(key => key is not null && ExperienceStatementCatalogue.TryGet(key, out var statement) && statement.Kind == kind)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENT_UNKNOWN"])
            .OverridePropertyName(name);
    }
}

public sealed class CandidateExperienceListQueryValidator : AbstractValidator<CandidateExperienceListQuery>
{
    public CandidateExperienceListQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}

public sealed class AdminCandidateExperienceListQueryValidator : AbstractValidator<AdminCandidateExperienceListQuery>
{
    public AdminCandidateExperienceListQueryValidator()
    {
        RuleFor(x => x.Company).MaximumLength(100);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000);
    }
}
