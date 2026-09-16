using AfterApply.Domain.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

public class CompanyReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Admin = Guid.CreateVersion7();

    private static ReviewContent LegacyContent(int overall = 4, string title = "Solid engineering culture") => new(
        EmploymentStatus.FormerEmployee, title,
        "Good tooling and honest code reviews across teams.",
        "Salary reviews lagged the market by a year or so.",
        overall, 4, 5, 3, 4);

    private static StructuredReviewContent Structured(int overall = 4,
        IReadOnlyList<CategoryRating>? categories = null, IReadOnlyList<string>? liked = null, IReadOnlyList<string>? improvable = null) =>
        new(EmploymentStatus.FormerEmployee, overall,
            categories ?? [new CategoryRating(ReviewCategory.WorkEnvironment, 5), new CategoryRating(ReviewCategory.Pay, 2)],
            liked ?? ["environment.pos.team_communication"],
            improvable ?? ["pay.imp.salary_level"]);

    [Fact]
    public void A_Structured_Review_Is_Published_On_Creation()
    {
        var review = CompanyReview.CreateStructured(Author, Guid.CreateVersion7(), Structured(), Now);

        review.Format.ShouldBe(ReviewFormat.Structured);
        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.SubmittedAt.ShouldBe(Now);
        review.ModeratedAt.ShouldBeNull();
        review.ModeratedByUserId.ShouldBeNull();
        review.OverallRating.ShouldBe(4);
        review.Title.ShouldBeNull();
        review.ManagementRating.ShouldBeNull();
    }

    [Fact]
    public void A_Legacy_Review_Is_Pending()
    {
        var review = CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(), Now);

        review.Format.ShouldBe(ReviewFormat.Legacy);
        review.Status.ShouldBe(ReviewModerationStatus.Pending);
        review.Title.ShouldBe("Solid engineering culture");
        review.ManagementRating.ShouldBe(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void An_Overall_Rating_Off_The_Scale_Is_Refused(int overall)
    {
        Should.Throw<ReviewContentInvalidException>(() =>
            CompanyReview.CreateStructured(Author, Guid.CreateVersion7(), Structured(overall), Now));
        Should.Throw<ReviewContentInvalidException>(() =>
            CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(overall), Now));
    }

    [Fact]
    public void Category_Ratings_Must_Be_On_The_Scale_And_Unique_And_Never_Overall()
    {
        Should.Throw<ReviewContentInvalidException>(() =>
            Structured(categories: [new CategoryRating(ReviewCategory.Pay, 0)]).Validate());
        Should.Throw<ReviewContentInvalidException>(() =>
            Structured(categories: [new CategoryRating(ReviewCategory.Pay, 3), new CategoryRating(ReviewCategory.Pay, 4)]).Validate());
        Should.Throw<ReviewContentInvalidException>(() =>
            Structured(categories: [new CategoryRating(ReviewCategory.Overall, 3)]).Validate());
        Should.NotThrow(() => Structured(categories: []).Validate());
    }

    [Fact]
    public void Statement_Picks_Must_Exist_Match_Their_List_And_Stay_Under_The_Cap()
    {
        Should.Throw<ReviewContentInvalidException>(() => Structured(liked: ["environment.pos.not_a_thing"]).Validate());
        // A "liked" key in the improvable list would publish the opposite of what was meant.
        Should.Throw<ReviewContentInvalidException>(() => Structured(improvable: ["environment.pos.team_communication"]).Validate());
        Should.Throw<ReviewContentInvalidException>(() =>
            Structured(liked: ["environment.pos.team_communication", "environment.pos.team_communication"]).Validate());

        var six = ReviewStatementCatalogue.For(ReviewCategory.WorkEnvironment, ReviewStatementKind.Liked).Take(6).Select(s => s.Key).ToList();
        Should.Throw<ReviewContentInvalidException>(() => Structured(liked: six).Validate());
        Should.NotThrow(() => Structured(liked: six.Take(5).ToList()).Validate());
        Should.NotThrow(() => Structured(liked: [], improvable: []).Validate());
    }

    [Fact]
    public void Blank_Legacy_Text_Is_Refused()
    {
        Should.Throw<ReviewContentInvalidException>(() =>
            CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(title: "   "), Now));
    }

    [Fact]
    public void Editing_A_Published_Review_Keeps_It_Published_And_Clears_Any_Decision()
    {
        var review = CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(), Now);
        review.Approve(Admin, Now.AddHours(1));

        review.EditStructured(Structured(overall: 2), Now.AddDays(1));

        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.Format.ShouldBe(ReviewFormat.Structured);
        review.ModeratedAt.ShouldBeNull();
        review.ModeratedByUserId.ShouldBeNull();
        review.RejectionReason.ShouldBeNull();
        review.SubmittedAt.ShouldBe(Now.AddDays(1));
        review.OverallRating.ShouldBe(2);
    }

    [Fact]
    public void Converting_A_Legacy_Review_Leaves_Its_Text_And_Old_Ratings_In_Place()
    {
        var review = CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(), Now);

        review.EditStructured(Structured(), Now.AddDays(1));

        // Hidden, never erased.
        review.Title.ShouldBe("Solid engineering culture");
        review.Pros.ShouldNotBeNull();
        review.ManagementRating.ShouldBe(4);
        review.Format.ShouldBe(ReviewFormat.Structured);
    }

    [Fact]
    public void Editing_A_Rejected_Review_Sends_It_To_Pending_Not_Straight_Back_Online()
    {
        var review = CompanyReview.CreateStructured(Author, Guid.CreateVersion7(), Structured(), Now);
        review.Reject(Admin, "Removed after a report.", Now.AddHours(1));

        review.EditStructured(Structured(), Now.AddDays(1));

        review.Status.ShouldBe(ReviewModerationStatus.Pending);
        review.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void Approve_Records_Who_And_When()
    {
        var review = CompanyReview.CreateLegacy(Author, Guid.CreateVersion7(), LegacyContent(), Now);

        review.Approve(Admin, Now.AddHours(1));

        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.ModeratedByUserId.ShouldBe(Admin);
        review.ModeratedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Reject_Requires_A_Reason()
    {
        var review = CompanyReview.CreateStructured(Author, Guid.CreateVersion7(), Structured(), Now);

        Should.Throw<ReviewRejectionReasonRequiredException>(() => review.Reject(Admin, "  ", Now));

        review.Reject(Admin, " Too vague to publish. ", Now);
        review.Status.ShouldBe(ReviewModerationStatus.Rejected);
        review.RejectionReason.ShouldBe("Too vague to publish.");
    }

    [Fact]
    public void Statements_Resolve_In_The_Order_They_Were_Picked()
    {
        var content = Structured(liked: ["environment.pos.motivating", "environment.pos.team_communication"], improvable: ["pay.imp.salary_level"]);

        content.Statements().Select(s => s.Key).ShouldBe(
            ["environment.pos.motivating", "environment.pos.team_communication", "pay.imp.salary_level"]);
        content.Statements().Select(s => s.Kind).ShouldBe([ReviewStatementKind.Liked, ReviewStatementKind.Liked, ReviewStatementKind.Improve]);
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
