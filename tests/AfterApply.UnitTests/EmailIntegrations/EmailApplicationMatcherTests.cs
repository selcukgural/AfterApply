using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.EmailIntegrations;

public class EmailApplicationMatcherTests
{
    [Fact]
    public void Match_Uses_Sender_Domain_Against_Company_Website_As_Primary_Signal()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        var result = EmailApplicationMatcher.Match(
            "recruiter@acme.com", "Jane at Acme", "me@example.com", "me@example.com", "Your application", candidates);

        result.ShouldBe(new EmailApplicationMatchResult(applicationId, EmailApplicationMatchType.DomainMatch));
    }

    [Fact]
    public void Match_Falls_Back_To_Company_Name_In_Sender_Display_Name_When_Website_Is_Null()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), null)
        };

        var result = EmailApplicationMatcher.Match(
            "noreply@greenhouse.io", "Acme Corp Recruiting", "me@example.com", "me@example.com",
            "Update on your application", candidates);

        result.ShouldBe(new EmailApplicationMatchResult(applicationId, EmailApplicationMatchType.NameFallbackMatch));
    }

    [Fact]
    public void Match_Falls_Back_To_Company_Name_In_Subject_When_Website_Is_Null()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), null)
        };

        var result = EmailApplicationMatcher.Match(
            "noreply@greenhouse.io", "Greenhouse", "me@example.com", "me@example.com",
            "Your application to Acme Corp", candidates);

        result.ShouldBe(new EmailApplicationMatchResult(applicationId, EmailApplicationMatchType.NameFallbackMatch));
    }

    [Fact]
    public void Match_Falls_Back_To_Name_Match_When_Website_Is_Set_But_Domain_Does_Not_Match()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        // Sent via a third-party ATS domain, not acme.com — primary signal doesn't match,
        // fallback name match against the subject should still find it.
        var result = EmailApplicationMatcher.Match(
            "noreply@greenhouse.io", "Greenhouse", "me@example.com", "me@example.com",
            "Your application to Acme Corp", candidates);

        result.ShouldBe(new EmailApplicationMatchResult(applicationId, EmailApplicationMatchType.NameFallbackMatch));
    }

    [Fact]
    public void Match_Returns_Null_When_Nothing_Matches()
    {
        var candidates = new[]
        {
            new ApplicationMatchCandidate(Guid.NewGuid(), CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        var result = EmailApplicationMatcher.Match(
            "noreply@other.com", "Other Company", "me@example.com", "me@example.com", "Newsletter", candidates);

        result.ShouldBeNull();
    }

    [Fact]
    public void Match_Uses_Recipient_Domain_When_Sender_Is_The_User_Themselves()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        // The user replying to their own recruiter, e.g. "I accept the offer" — sender is the
        // user's own account, so the recipient (the recruiter) is what should be matched.
        var result = EmailApplicationMatcher.Match(
            "me@example.com", "Selçuk Güral", "recruiter@acme.com", "me@example.com",
            "Re: Offer letter", candidates);

        result.ShouldBe(new EmailApplicationMatchResult(applicationId, EmailApplicationMatchType.DomainMatch));
    }

    [Fact]
    public void Match_Self_Sent_Returns_Null_When_Recipient_Domain_Does_Not_Match_And_Display_Name_Is_Not_Used()
    {
        var applicationId = Guid.NewGuid();
        var candidates = new[]
        {
            new ApplicationMatchCandidate(applicationId, CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        // Self-sent, recipient domain doesn't match, and the subject doesn't mention the company —
        // the sender display name (the user's own name) must NOT be used as a fallback signal here.
        var result = EmailApplicationMatcher.Match(
            "me@example.com", "Selçuk Güral", "someone@unrelated.com", "me@example.com",
            "Re: dinner plans", candidates);

        result.ShouldBeNull();
    }
}
