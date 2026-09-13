using AfterApply.Domain.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

public class CompanyReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Admin = Guid.CreateVersion7();

    private static ReviewContent Content(int overall = 4, string title = "Solid engineering culture") => new(
        EmploymentStatus.FormerEmployee, title,
        "Good tooling and honest code reviews across teams.",
        "Salary reviews lagged the market by a year or so.",
        overall, 4, 5, 3, 4);

    [Fact]
    public void A_New_Review_Is_Pending()
    {
        var review = CompanyReview.Create(Author, Guid.CreateVersion7(), Content(), Now);

        review.Status.ShouldBe(ReviewModerationStatus.Pending);
        review.SubmittedAt.ShouldBe(Now);
        review.ModeratedAt.ShouldBeNull();
        review.Title.ShouldBe("Solid engineering culture");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Ratings_Outside_One_To_Five_Are_Refused(int overall)
    {
        Should.Throw<ReviewContentInvalidException>(() =>
            CompanyReview.Create(Author, Guid.CreateVersion7(), Content(overall), Now));
    }

    [Fact]
    public void Blank_Text_Is_Refused()
    {
        Should.Throw<ReviewContentInvalidException>(() =>
            CompanyReview.Create(Author, Guid.CreateVersion7(), Content(title: "   "), Now));
    }

    [Fact]
    public void Editing_An_Approved_Review_Sends_It_Back_To_Pending_And_Clears_The_Decision()
    {
        var review = CompanyReview.Create(Author, Guid.CreateVersion7(), Content(), Now);
        review.Approve(Admin, Now.AddHours(1));

        review.Edit(Content(overall: 2, title: "Changed my mind"), Now.AddDays(1));

        review.Status.ShouldBe(ReviewModerationStatus.Pending);
        review.ModeratedAt.ShouldBeNull();
        review.ModeratedByUserId.ShouldBeNull();
        review.RejectionReason.ShouldBeNull();
        review.SubmittedAt.ShouldBe(Now.AddDays(1));
        review.OverallRating.ShouldBe(2);
    }

    [Fact]
    public void Editing_A_Rejected_Review_Drops_The_Old_Reason()
    {
        var review = CompanyReview.Create(Author, Guid.CreateVersion7(), Content(), Now);
        review.Reject(Admin, "Names a colleague.", Now.AddHours(1));

        review.Edit(Content(), Now.AddDays(1));

        review.Status.ShouldBe(ReviewModerationStatus.Pending);
        review.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void Approve_Records_Who_And_When()
    {
        var review = CompanyReview.Create(Author, Guid.CreateVersion7(), Content(), Now);

        review.Approve(Admin, Now.AddHours(1));

        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.ModeratedByUserId.ShouldBe(Admin);
        review.ModeratedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Reject_Requires_A_Reason()
    {
        var review = CompanyReview.Create(Author, Guid.CreateVersion7(), Content(), Now);

        Should.Throw<ReviewRejectionReasonRequiredException>(() => review.Reject(Admin, "  ", Now));

        review.Reject(Admin, " Too vague to publish. ", Now);
        review.Status.ShouldBe(ReviewModerationStatus.Rejected);
        review.RejectionReason.ShouldBe("Too vague to publish.");
    }
}

public class CompanyReviewReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Other_Needs_A_Note()
    {
        Should.Throw<ReviewReportNoteRequiredException>(() =>
            CompanyReviewReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ReviewReportReason.Other, " ", Now));

        CompanyReviewReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ReviewReportReason.Spam, null, Now)
            .Status.ShouldBe(ReviewReportStatus.Open);
    }

    [Fact]
    public void Dismissing_Needs_No_Reason_But_The_Other_Two_Do()
    {
        var admin = Guid.CreateVersion7();
        var dismissed = CompanyReviewReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ReviewReportReason.Spam, null, Now);
        dismissed.Resolve(admin, ReviewReportResolution.Dismissed, null, Now);
        dismissed.Status.ShouldBe(ReviewReportStatus.Resolved);
        dismissed.Resolution.ShouldBe(ReviewReportResolution.Dismissed);

        var removed = CompanyReviewReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ReviewReportReason.Insult, null, Now);
        Should.Throw<ReviewReportResolutionReasonRequiredException>(() =>
            removed.Resolve(admin, ReviewReportResolution.Removed, "", Now));
    }

    [Fact]
    public void A_Report_Is_Resolved_Once()
    {
        var admin = Guid.CreateVersion7();
        var report = CompanyReviewReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ReviewReportReason.Spam, null, Now);
        report.Resolve(admin, ReviewReportResolution.Dismissed, null, Now);

        Should.Throw<ReviewReportAlreadyResolvedException>(() =>
            report.Resolve(admin, ReviewReportResolution.Removed, "again", Now));
    }
}
