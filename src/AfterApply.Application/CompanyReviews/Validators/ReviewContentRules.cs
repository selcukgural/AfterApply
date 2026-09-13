using AfterApply.Domain.CompanyReviews;
using FluentValidation;

namespace AfterApply.Application.CompanyReviews.Validators;

/// <summary>The rules Create and Update share, applied field by field so the ProblemDetails names
/// the field the form has to highlight.</summary>
internal static class ReviewContentRules
{
    /// <summary>Short enough to reject "good" and "bad", long enough for one honest sentence.</summary>
    public const int MinTextLength = 20;
    public const int MinTitleLength = 3;

    public static void Apply<T>(AbstractValidator<T> validator,
        Func<T, EmploymentStatus> employmentStatus,
        Func<T, string> title, Func<T, string> pros, Func<T, string> cons,
        Func<T, int> overall, Func<T, int> management, Func<T, int> workEnvironment,
        Func<T, int> salaryAndBenefits, Func<T, int> careerAndDevelopment)
    {
        validator.RuleFor(x => employmentStatus(x)).IsInEnum().OverridePropertyName("EmploymentStatus");
        validator.RuleFor(x => title(x)).NotEmpty().Length(MinTitleLength, CompanyReview.MaxTitleLength).OverridePropertyName("Title");
        validator.RuleFor(x => pros(x)).NotEmpty().Length(MinTextLength, CompanyReview.MaxTextLength).OverridePropertyName("Pros");
        validator.RuleFor(x => cons(x)).NotEmpty().Length(MinTextLength, CompanyReview.MaxTextLength).OverridePropertyName("Cons");
        validator.RuleFor(x => overall(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("OverallRating");
        validator.RuleFor(x => management(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("ManagementRating");
        validator.RuleFor(x => workEnvironment(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("WorkEnvironmentRating");
        validator.RuleFor(x => salaryAndBenefits(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("SalaryAndBenefitsRating");
        validator.RuleFor(x => careerAndDevelopment(x)).InclusiveBetween(CompanyReview.MinRating, CompanyReview.MaxRating).OverridePropertyName("CareerAndDevelopmentRating");
    }
}

public sealed class CreateCompanyReviewRequestValidator : AbstractValidator<Contracts.CreateCompanyReviewRequest>
{
    public CreateCompanyReviewRequestValidator()
    {
        ReviewContentRules.Apply(this, x => x.EmploymentStatus, x => x.Title, x => x.Pros, x => x.Cons,
            x => x.OverallRating, x => x.ManagementRating, x => x.WorkEnvironmentRating,
            x => x.SalaryAndBenefitsRating, x => x.CareerAndDevelopmentRating);
    }
}

public sealed class UpdateCompanyReviewRequestValidator : AbstractValidator<Contracts.UpdateCompanyReviewRequest>
{
    public UpdateCompanyReviewRequestValidator()
    {
        ReviewContentRules.Apply(this, x => x.EmploymentStatus, x => x.Title, x => x.Pros, x => x.Cons,
            x => x.OverallRating, x => x.ManagementRating, x => x.WorkEnvironmentRating,
            x => x.SalaryAndBenefitsRating, x => x.CareerAndDevelopmentRating);
    }
}
