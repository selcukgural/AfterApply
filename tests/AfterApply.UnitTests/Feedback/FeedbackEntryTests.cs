using AfterApply.Domain.Feedback;
using Shouldly;

namespace AfterApply.UnitTests.Feedback;

public class FeedbackEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static FeedbackEntry CreateEntry() => FeedbackEntry.Create(
        userId: Guid.CreateVersion7(),
        category: FeedbackCategory.Idea,
        mood: FeedbackMood.Good,
        message: "Let me group applications by company.",
        replyEmail: "someone@example.com",
        pagePath: "/tr/applications",
        locale: "tr",
        theme: "dark",
        userAgent: "Mozilla/5.0",
        now: Now);

    [Fact]
    public void A_New_Entry_Starts_Unanswered_And_Unmirrored()
    {
        var entry = CreateEntry();

        entry.Status.ShouldBe(FeedbackStatus.Received);
        entry.SubmittedAt.ShouldBe(Now);
        entry.AdminReply.ShouldBeNull();
        entry.AdminReplyAt.ShouldBeNull();
        entry.GitHubIssueNumber.ShouldBeNull();
        entry.MirroredAt.ShouldBeNull();
    }

    [Fact]
    public void Recording_The_Mirrored_Issue_Makes_It_Findable_From_The_Database_Side()
    {
        var entry = CreateEntry();
        var mirroredAt = Now.AddSeconds(3);

        entry.RecordMirroredIssue(412, "https://github.com/owner/repo/issues/412", mirroredAt);

        entry.GitHubIssueNumber.ShouldBe(412);
        entry.GitHubIssueUrl.ShouldBe("https://github.com/owner/repo/issues/412");
        entry.MirroredAt.ShouldBe(mirroredAt);
        entry.UpdatedAt.ShouldBe(mirroredAt);
    }
}
