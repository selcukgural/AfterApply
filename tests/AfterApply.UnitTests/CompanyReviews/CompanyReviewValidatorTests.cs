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

    private static CreateCompanyReviewRequest Create(string title = "Fair place to grow", string pros = "Clear promotion criteria and patient mentors.",
        string cons = "Meetings could easily be halved without loss.", int overall = 4) =>
        new(EmploymentStatus.CurrentEmployee, title, pros, cons, overall, 4, 4, 3, 5);

    [Fact]
    public void Accepts_A_Complete_Review()
    {
        new CreateCompanyReviewRequestValidator().Validate(Create()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rejects_A_Rating_Off_The_Scale(int overall)
    {
        var result = new CreateCompanyReviewRequestValidator().Validate(Create(overall: overall));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "OverallRating");
    }

    [Fact]
    public void Rejects_Pros_Too_Short_To_Say_Anything()
    {
        var result = new CreateCompanyReviewRequestValidator().Validate(Create(pros: "Nice."));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Pros");
    }

    [Fact]
    public void Rejects_Text_Past_The_Column_Width()
    {
        var tooLong = new string('a', CompanyReview.MaxTextLength + 1);

        new UpdateCompanyReviewRequestValidator()
            .Validate(new UpdateCompanyReviewRequest(EmploymentStatus.Intern, "Title", tooLong, "Cons that are long enough.", 3, 3, 3, 3, 3))
            .IsValid.ShouldBeFalse();
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
