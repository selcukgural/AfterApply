using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanyReviews.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.CompanyReviews;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

public class CompanyReviewValidatorTests
{
    private static IStringLocalizer<SharedStrings> Localizer() => new KeyEchoLocalizer();

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static CreateCompanyReviewRequest Create(int overall = 4,
        IReadOnlyList<ReviewCategoryRatingDto>? categories = null, IReadOnlyList<string>? liked = null, IReadOnlyList<string>? improvable = null) =>
        new(EmploymentStatus.CurrentEmployee, overall,
            categories ?? [new ReviewCategoryRatingDto(ReviewCategory.CareerGrowth, 5)],
            liked ?? ["career.pos.career_guidance"],
            improvable ?? ["balance.imp.overtime"]);

    private static CreateCompanyReviewRequestValidator Validator() => new(Localizer());

    [Fact]
    public void Accepts_A_Complete_Review()
    {
        Validator().Validate(Create()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_A_Bare_Overall_Rating()
    {
        // The minimum review: a number and a relationship, nothing else.
        Validator().Validate(new CreateCompanyReviewRequest(EmploymentStatus.Intern, 3)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rejects_A_Rating_Off_The_Scale(int overall)
    {
        var result = Validator().Validate(Create(overall: overall));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "OverallRating");
    }

    [Fact]
    public void Rejects_A_Category_Rated_Twice_Or_Off_The_Scale_Or_Named_Overall()
    {
        Validator().Validate(Create(categories: [new(ReviewCategory.Pay, 2), new(ReviewCategory.Pay, 4)]))
            .Errors.ShouldContain(e => e.PropertyName == "CategoryRatings" && e.ErrorMessage == "VALIDATION_REVIEW_CATEGORY_DUPLICATE");
        Validator().Validate(Create(categories: [new(ReviewCategory.Pay, 0)]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("CategoryRatings"));
        Validator().Validate(Create(categories: [new(ReviewCategory.Overall, 3)]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("CategoryRatings"));
    }

    [Fact]
    public void Rejects_More_Than_Five_Picks_Per_List_With_A_Localised_Message()
    {
        var six = ReviewStatementCatalogue.For(ReviewCategory.WorkEnvironment, ReviewStatementKind.Liked).Take(6).Select(s => s.Key).ToList();

        var result = Validator().Validate(Create(liked: six));

        result.Errors.ShouldContain(e => e.PropertyName == "LikedStatements" && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENTS_TOO_MANY");
        Validator().Validate(Create(liked: six.Take(5).ToList())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_Unknown_Keys_Duplicates_And_Keys_From_The_Other_List()
    {
        Validator().Validate(Create(liked: ["career.pos.made_up"]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("LikedStatements") && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENT_UNKNOWN");
        Validator().Validate(Create(improvable: ["career.pos.career_guidance"]))
            .Errors.ShouldContain(e => e.PropertyName.StartsWith("ImprovableStatements") && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENT_UNKNOWN");
        Validator().Validate(Create(improvable: ["balance.imp.overtime", "balance.imp.overtime"]))
            .Errors.ShouldContain(e => e.PropertyName == "ImprovableStatements" && e.ErrorMessage == "VALIDATION_REVIEW_STATEMENTS_DUPLICATE");
    }

    [Fact]
    public void The_Update_Validator_Applies_The_Same_Rules()
    {
        new UpdateCompanyReviewRequestValidator(Localizer())
            .Validate(new UpdateCompanyReviewRequest(EmploymentStatus.Intern, 3, LikedStatements: ["nope"]))
            .IsValid.ShouldBeFalse();
        new UpdateCompanyReviewRequestValidator(Localizer())
            .Validate(new UpdateCompanyReviewRequest(EmploymentStatus.Intern, 3))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_Report_With_Reason_Other_Needs_A_Note()
    {
        var validator = new ReportCompanyReviewRequestValidator(Localizer());

        validator.Validate(new ReportCompanyReviewRequest(ReviewReportReason.Other)).IsValid.ShouldBeFalse();
        validator.Validate(new ReportCompanyReviewRequest(ReviewReportReason.Other, "It quotes a private Slack message.")).IsValid.ShouldBeTrue();
        validator.Validate(new ReportCompanyReviewRequest(ReviewReportReason.Spam)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Resolving_With_Anything_But_Dismissed_Needs_A_Reason()
    {
        var validator = new ResolveReviewReportRequestValidator(Localizer());

        validator.Validate(new ResolveReviewReportRequest(ReviewReportResolution.Dismissed)).IsValid.ShouldBeTrue();
        validator.Validate(new ResolveReviewReportRequest(ReviewReportResolution.Removed)).IsValid.ShouldBeFalse();
        validator.Validate(new ResolveReviewReportRequest(ReviewReportResolution.ChangesRequested, "Remove the manager's name.")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Quota_Override_Is_Bounded_And_Null_Is_Allowed()
    {
        var validator = new SetReviewQuotaRequestValidator();

        validator.Validate(new SetReviewQuotaRequest(null)).IsValid.ShouldBeTrue();
        validator.Validate(new SetReviewQuotaRequest(0)).IsValid.ShouldBeTrue();
        validator.Validate(new SetReviewQuotaRequest(1001)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Admin_List_Rejects_An_Inverted_Date_Range()
    {
        var validator = new AdminReviewListQueryValidator();
        var now = DateTimeOffset.UtcNow;

        validator.Validate(new AdminReviewListQuery(From: now, To: now.AddDays(-1))).IsValid.ShouldBeFalse();
        validator.Validate(new AdminReviewListQuery(From: now.AddDays(-1), To: now)).IsValid.ShouldBeTrue();
    }
}
