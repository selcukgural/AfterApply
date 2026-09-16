using AfterApply.Application.Localization;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.CompanyReviews;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AfterApply.Application.CompanyReviews.Validators;

/// <summary>The rules Create and Update share, applied field by field so the ProblemDetails names
/// the field the form has to highlight. The domain's <see cref="StructuredReviewContent.Validate"/>
/// repeats them at the boundary that stores the row; this layer is the one that speaks the
/// user's language.</summary>
internal static class StructuredReviewRules
{
    public static void Apply<T>(AbstractValidator<T> validator, IStringLocalizer<SharedStrings> localizer,
        Func<T, EmploymentStatus> employmentStatus,
        Func<T, int> overall,
        Func<T, IReadOnlyList<ReviewCategoryRatingDto>?> categoryRatings,
        Func<T, IReadOnlyList<string>?> liked,
        Func<T, IReadOnlyList<string>?> improvable)
    {
        validator.RuleFor(x => employmentStatus(x)).IsInEnum().OverridePropertyName("EmploymentStatus");
        validator.RuleFor(x => overall(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("OverallRating");

        validator.RuleFor(x => categoryRatings(x))
            .Must(list => list is null || list.Select(r => r.Category).Distinct().Count() == list.Count)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_CATEGORY_DUPLICATE"])
            .OverridePropertyName("CategoryRatings");
        validator.RuleForEach(x => categoryRatings(x))
            .ChildRules(rating =>
            {
                // Overall is its own field, never one of these.
                rating.RuleFor(r => r.Category).IsInEnum().NotEqual(ReviewCategory.Overall);
                rating.RuleFor(r => r.Rating).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating);
            })
            .OverridePropertyName("CategoryRatings");

        Picks(validator, localizer, liked, ReviewStatementKind.Liked, "LikedStatements");
        Picks(validator, localizer, improvable, ReviewStatementKind.Improve, "ImprovableStatements");
    }

    private static void Picks<T>(AbstractValidator<T> validator, IStringLocalizer<SharedStrings> localizer,
        Func<T, IReadOnlyList<string>?> picks, ReviewStatementKind kind, string name)
    {
        validator.RuleFor(x => picks(x))
            .Must(list => list is null || list.Count <= ReviewStatementCatalogue.MaxPicksPerKind)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENTS_TOO_MANY", ReviewStatementCatalogue.MaxPicksPerKind])
            .Must(list => list is null || list.Distinct(StringComparer.Ordinal).Count() == list.Count)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENTS_DUPLICATE"])
            .OverridePropertyName(name);
        // A key must exist and be of the list's kind: a "liked" sentence in the "improve" list
        // would publish the opposite of what the author meant.
        validator.RuleForEach(x => picks(x))
            .Must(key => key is not null && ReviewStatementCatalogue.TryGet(key, out var statement) && statement.Kind == kind)
            .WithMessage(_ => localizer["VALIDATION_REVIEW_STATEMENT_UNKNOWN"])
            .OverridePropertyName(name);
    }
}

public sealed class CreateCompanyReviewRequestValidator : AbstractValidator<CreateCompanyReviewRequest>
{
    public CreateCompanyReviewRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        StructuredReviewRules.Apply(this, localizer, x => x.EmploymentStatus, x => x.OverallRating,
            x => x.CategoryRatings, x => x.LikedStatements, x => x.ImprovableStatements);
    }
}

public sealed class UpdateCompanyReviewRequestValidator : AbstractValidator<UpdateCompanyReviewRequest>
{
    public UpdateCompanyReviewRequestValidator(IStringLocalizer<SharedStrings> localizer)
    {
        StructuredReviewRules.Apply(this, localizer, x => x.EmploymentStatus, x => x.OverallRating,
            x => x.CategoryRatings, x => x.LikedStatements, x => x.ImprovableStatements);
    }
}
