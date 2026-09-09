using AfterApply.Infrastructure.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

// The sign-in endpoints log the redirect URI they rejected, straight from the request body. These
// pin the one property that keeps that line honest.
public class SafeLogValueTests
{
    [Fact]
    public void A_Redirect_Uri_With_An_Embedded_Newline_Really_Does_Survive_Uri_Parsing()
    {
        // The premise of the whole helper: this is not a value the validator can be relied on to
        // stop. Uri accepts it as an absolute URL and keeps the newline.
        const string forged = "https://evil.example.com/x\nGitHub sign-in succeeded";

        Uri.TryCreate(forged, UriKind.Absolute, out var parsed).ShouldBeTrue();
        parsed!.ToString().ShouldContain("\n");
    }

    [Theory]
    [InlineData("https://evil.example.com/x\nGitHub sign-in succeeded")]
    [InlineData("https://evil.example.com/x\r\nGitHub sign-in succeeded")]
    [InlineData("https://evil.example.com/x\rGitHub sign-in succeeded")]
    public void Strips_Every_Line_Break_So_One_Value_Can_Never_Become_Two_Log_Lines(string forged)
    {
        var safe = SafeLogValue.SingleLine(forged);

        safe.ShouldNotContain("\n");
        safe.ShouldNotContain("\r");
    }

    [Fact]
    public void Keeps_Everything_Else_Verbatim()
    {
        // The point of logging the value at all is recognising a mistyped origin, so nothing but the
        // line breaks may be lost.
        const string ordinary = "https://ekariyerim.com/tr/auth/github/callback?x=1&y=%20";

        SafeLogValue.SingleLine(ordinary).ShouldBe(ordinary);
    }

    [Fact]
    public void Leaves_An_Empty_Value_Empty()
    {
        SafeLogValue.SingleLine(string.Empty).ShouldBe(string.Empty);
    }
}
