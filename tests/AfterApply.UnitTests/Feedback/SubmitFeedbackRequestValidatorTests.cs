using AfterApply.Application.Feedback.Contracts;
using AfterApply.Application.Feedback.Validators;
using AfterApply.Domain.Feedback;
using Shouldly;

namespace AfterApply.UnitTests.Feedback;

public class SubmitFeedbackRequestValidatorTests
{
    private readonly SubmitFeedbackRequestValidator _validator = new();

    private static SubmitFeedbackRequest Request(string message = "The status filter forgets my choice.",
        string? replyEmail = null, string? pagePath = "/tr/applications", FeedbackMood? mood = FeedbackMood.Okay) =>
        new(FeedbackCategory.Bug, message, mood, replyEmail, pagePath, "tr", "dark");

    [Fact]
    public void Accepts_A_Filled_In_Panel()
    {
        _validator.Validate(Request(replyEmail: "someone@example.com")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_A_Message_With_No_Mood_And_No_Reply_Address()
    {
        _validator.Validate(Request(mood: null)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_An_Empty_Message(string message)
    {
        _validator.Validate(Request(message)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_A_Message_Past_The_Column_Width()
    {
        var tooLong = new string('a', SubmitFeedbackRequestValidator.MaxMessageLength + 1);

        _validator.Validate(Request(tooLong)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Accepts_A_Message_Exactly_At_The_Limit()
    {
        var atLimit = new string('a', SubmitFeedbackRequestValidator.MaxMessageLength);

        _validator.Validate(Request(atLimit)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_A_Reply_Address_That_Is_Not_An_Address()
    {
        _validator.Validate(Request(replyEmail: "not-an-address")).IsValid.ShouldBeFalse();
    }

    [Theory]
    // A whole URL, not a path — the client has no business sending an origin.
    [InlineData("https://ekariyerim.com/tr/applications")]
    [InlineData("tr/applications")]
    public void Rejects_A_Page_Context_That_Is_Not_A_Path(string pagePath)
    {
        _validator.Validate(Request(pagePath: pagePath)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_A_Category_Outside_The_Enum()
    {
        var request = Request() with { Category = (FeedbackCategory)99 };

        _validator.Validate(request).IsValid.ShouldBeFalse();
    }
}
