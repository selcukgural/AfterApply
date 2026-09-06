using AfterApply.Application.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

// This gate decides both what gets stored and what gets offered, so a false positive is worse than
// a miss: a no-reply address recorded as "who to contact" only reveals itself as wrong after the
// user writes to it and hears nothing back.
public class HrEmailCandidateTests
{
    [Theory]
    [InlineData("ayse.yilmaz@company.com")]
    // A departmental mailbox is not a named person but is monitored by one, which is what a
    // candidate actually needs.
    [InlineData("careers@company.com")]
    [InlineData("ik@sirket.com.tr")]
    [InlineData("hiring@company.co.uk")]
    public void Accepts_A_Mailbox_Someone_Could_Write_Back_To(string email)
    {
        HrEmailCandidate.From(email, senderIsKnownJobBoard: false).ShouldBe(email);
    }

    [Theory]
    [InlineData("no-reply@company.com")]
    [InlineData("noreply@company.com")]
    [InlineData("no.reply@company.com")]
    [InlineData("do-not-reply@company.com")]
    [InlineData("donotreply@company.com")]
    [InlineData("jobs-noreply@company.com")]
    [InlineData("bounce-1234@company.com")]
    [InlineData("mailer-daemon@company.com")]
    [InlineData("postmaster@company.com")]
    [InlineData("notifications@company.com")]
    [InlineData("auto-reply@company.com")]
    public void Rejects_An_Automated_Mailbox(string email)
    {
        HrEmailCandidate.From(email, senderIsKnownJobBoard: false).ShouldBeNull();
    }

    [Fact]
    public void Rejects_A_Known_Job_Board_Or_Ats_Sender()
    {
        // The address belongs to the tooling vendor, not to the company the user applied to —
        // replying reaches nobody who can help.
        HrEmailCandidate.From("careers@greenhouse.io", senderIsKnownJobBoard: true).ShouldBeNull();
    }

    [Fact]
    public void Normalizes_Case_And_Surrounding_Whitespace()
    {
        HrEmailCandidate.From("  Ayse.Yilmaz@Company.COM ", senderIsKnownJobBoard: false)
            .ShouldBe("ayse.yilmaz@company.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@company.com")]
    [InlineData("ayse@localhost")]
    [InlineData("ayse@.com")]
    [InlineData("a@b@c.com")]
    public void Rejects_Anything_That_Is_Not_A_Single_Ordinary_Address(string? email)
    {
        HrEmailCandidate.From(email, senderIsKnownJobBoard: false).ShouldBeNull();
    }

    [Theory]
    // These would change the meaning of the mailto: link the web app builds from the stored value.
    [InlineData("ayse@company.com,mallory@evil.example")]
    [InlineData("ayse@company.com?bcc=mallory@evil.example")]
    [InlineData("ayse@company.com&subject=x")]
    [InlineData("Ayse <ayse@company.com>")]
    public void Rejects_An_Address_We_Would_Refuse_To_Render(string email)
    {
        HrEmailCandidate.From(email, senderIsKnownJobBoard: false).ShouldBeNull();
    }

    [Fact]
    public void Rejects_An_Address_Longer_Than_The_Column()
    {
        var tooLong = new string('a', 310) + "@company.com";
        HrEmailCandidate.From(tooLong, senderIsKnownJobBoard: false).ShouldBeNull();
    }
}
