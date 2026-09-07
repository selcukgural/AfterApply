using AfterApply.Application.Feedback;
using AfterApply.Domain.Feedback;
using Shouldly;

namespace AfterApply.UnitTests.Feedback;

// The mirrored issue is user-written text rendered as Markdown by a third party, and it leaves our
// database. These tests pin the two properties that follow from that: the message can't act as
// markup, and nothing identifying the sender travels with it.
public class GitHubIssueComposerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static FeedbackEntry Entry(string message, FeedbackCategory category = FeedbackCategory.Bug,
        string? replyEmail = null, string? userAgent = "Mozilla/5.0") =>
        FeedbackEntry.Create(Guid.CreateVersion7(), category, FeedbackMood.Okay, message, replyEmail,
            "/tr/applications", "tr", "dark", userAgent, Now);

    [Fact]
    public void The_Title_Is_The_First_Line_Behind_The_Category()
    {
        var title = GitHubIssueComposer.Title(Entry("Status filter resets\nEvery time I go back."));

        title.ShouldBe("[Bug] Status filter resets");
    }

    [Fact]
    public void A_Long_First_Line_Is_Truncated_Rather_Than_Wrapped_Into_The_Title()
    {
        var title = GitHubIssueComposer.Title(Entry(new string('a', 200)));

        title.Length.ShouldBeLessThan(90);
        title.ShouldEndWith("…");
    }

    [Fact]
    public void A_Blank_Message_Still_Produces_A_Usable_Title()
    {
        GitHubIssueComposer.Title(Entry("\nsecond line")).ShouldBe("[Bug] (no summary)");
    }

    [Fact]
    public void The_Message_Is_Fenced_So_Markdown_In_It_Stays_Inert()
    {
        var body = GitHubIssueComposer.Body(Entry("# Not a heading @someone #123"));

        body.ShouldStartWith("```\n# Not a heading @someone #123\n```");
    }

    [Fact]
    public void A_Pasted_Fence_Cannot_Close_The_Block_Early()
    {
        // Three backticks of their own would end a three-backtick fence, and everything the user
        // wrote after it would render as Markdown — mentions included.
        var body = GitHubIssueComposer.Body(Entry("before\n```\n@everyone\nafter"));

        body.ShouldStartWith("````\n");
        body.ShouldContain("````\n\n|");
    }

    [Fact]
    public void The_Reply_Address_Never_Leaves_The_Database()
    {
        var body = GitHubIssueComposer.Body(Entry("Please get back to me.", replyEmail: "private@example.com"));

        body.ShouldNotContain("private@example.com");
        // But the reader has to know an answer is expected, and where to find the address.
        body.ShouldContain("Wants a reply | yes");
    }

    [Fact]
    public void The_Feedback_Id_Is_Carried_So_The_Real_Row_Can_Be_Found()
    {
        var entry = Entry("Something broke.");

        GitHubIssueComposer.Body(entry).ShouldContain(entry.Id.ToString());
    }

    [Fact]
    public void A_Pipe_In_The_Metadata_Cannot_Split_The_Table_Row()
    {
        var body = GitHubIssueComposer.Body(Entry("Hi", userAgent: "Mozilla/5.0 | injected | cells"));

        body.ShouldContain(@"Mozilla/5.0 \| injected \| cells");
    }

    [Theory]
    [InlineData(FeedbackCategory.Bug, "feedback:bug")]
    [InlineData(FeedbackCategory.Idea, "feedback:idea")]
    [InlineData(FeedbackCategory.Question, "feedback:question")]
    public void Each_Category_Gets_Its_Own_Triage_Label(FeedbackCategory category, string expected)
    {
        GitHubIssueComposer.Label(category).ShouldBe(expected);
    }
}
