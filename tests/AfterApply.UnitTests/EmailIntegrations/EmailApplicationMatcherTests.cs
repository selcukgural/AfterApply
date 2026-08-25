using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.Companies;
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
            "recruiter@acme.com", "Jane at Acme", "Your application", candidates);

        result.ShouldBe(applicationId);
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
            "noreply@greenhouse.io", "Acme Corp Recruiting", "Update on your application", candidates);

        result.ShouldBe(applicationId);
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
            "noreply@greenhouse.io", "Greenhouse", "Your application to Acme Corp", candidates);

        result.ShouldBe(applicationId);
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
            "noreply@greenhouse.io", "Greenhouse", "Your application to Acme Corp", candidates);

        result.ShouldBe(applicationId);
    }

    [Fact]
    public void Match_Returns_Null_When_Nothing_Matches()
    {
        var candidates = new[]
        {
            new ApplicationMatchCandidate(Guid.NewGuid(), CompanyNameNormalizer.Normalize("Acme Corp"), "acme.com")
        };

        var result = EmailApplicationMatcher.Match(
            "noreply@other.com", "Other Company", "Newsletter", candidates);

        result.ShouldBeNull();
    }
}
