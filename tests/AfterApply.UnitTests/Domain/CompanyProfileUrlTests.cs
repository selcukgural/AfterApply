using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class CompanyProfileUrlTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/company/trendyol/", "https://linkedin.com/company/trendyol")]
    [InlineData("https://LinkedIn.com/company/Trendyol?trk=abc#about", "https://linkedin.com/company/trendyol")]
    [InlineData("https://www.kariyer.net/firma-profil/borusan-lojistik-1166-1810", "https://kariyer.net/firma-profil/borusan-lojistik-1166-1810")]
    public void Two_Spellings_Of_The_Same_Page_Agree(string url, string canonical) =>
        CompanyProfileUrl.Canonical(url).ShouldBe(canonical);

    [Fact]
    public void A_Website_Remembers_Which_Page_It_Was_Read_From()
    {
        var company = Company.Create("Acme", DateTimeOffset.UtcNow, linkedInUrl: "https://www.linkedin.com/company/acme");
        company.EnrichFrom(Source.KariyerNet, "https://acme.example", "Lojistik", null, DateTimeOffset.UtcNow);

        company.Website.ShouldBe("https://acme.example");
        company.WebsiteSource.ShouldBe(Source.KariyerNet);
    }

    [Fact]
    public void A_Link_To_Another_Companys_Page_Can_Be_Dropped()
    {
        var company = Company.Create("Acme", DateTimeOffset.UtcNow,
            linkedInUrl: "https://www.linkedin.com/company/not-acme", kariyerNetUrl: "https://www.kariyer.net/firma-profil/acme-1-2");

        company.ClearProfileLink(Source.LinkedIn, DateTimeOffset.UtcNow);

        company.LinkedInUrl.ShouldBeNull();
        company.KariyerNetUrl.ShouldNotBeNull();
        // The emptied slot can be filled by a later capture.
        company.SetProfileLinksIfMissing("https://www.linkedin.com/company/acme", null, DateTimeOffset.UtcNow);
        company.LinkedInUrl.ShouldBe("https://www.linkedin.com/company/acme");
    }
}
