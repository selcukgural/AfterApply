using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class CompanyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private static Company NewCompany() => Company.Create("Acme", Now);

    [Fact]
    public void SetProfileLinksIfMissing_Fills_Each_Link_Independently()
    {
        // A company can pick up its kariyer.net profile long after its LinkedIn one, or the other
        // way round — the two sites are never seen in the same posting.
        var company = NewCompany();

        company.SetProfileLinksIfMissing("https://www.linkedin.com/company/acme/", null, Now);
        company.LinkedInUrl.ShouldBe("https://www.linkedin.com/company/acme/");
        company.KariyerNetUrl.ShouldBeNull();

        company.SetProfileLinksIfMissing(null, "https://www.kariyer.net/firma-profil/acme-1-2", Now);
        company.LinkedInUrl.ShouldBe("https://www.linkedin.com/company/acme/");
        company.KariyerNetUrl.ShouldBe("https://www.kariyer.net/firma-profil/acme-1-2");
    }

    [Fact]
    public void SetProfileLinksIfMissing_Never_Overwrites_What_Is_Already_Stored()
    {
        // Whatever got there first — a manual entry, an earlier import — wins over a later guess
        // scraped off a page.
        var company = NewCompany();
        company.SetProfileLinksIfMissing("https://www.linkedin.com/company/first/", "https://www.kariyer.net/firma-profil/first-1-2", Now);

        company.SetProfileLinksIfMissing("https://www.linkedin.com/company/second/", "https://www.kariyer.net/firma-profil/second-3-4", Now);

        company.LinkedInUrl.ShouldBe("https://www.linkedin.com/company/first/");
        company.KariyerNetUrl.ShouldBe("https://www.kariyer.net/firma-profil/first-1-2");
    }

    [Fact]
    public void SetProfileLinksIfMissing_Does_Not_Touch_The_Row_When_Nothing_Changes()
    {
        var company = NewCompany();
        var before = company.UpdatedAt;

        company.SetProfileLinksIfMissing(null, null, Now.AddDays(1));

        company.UpdatedAt.ShouldBe(before);
    }

    [Fact]
    public void EnrichFrom_Fills_Only_The_Gaps()
    {
        // kariyer.net runs after LinkedIn and must not clobber what LinkedIn already resolved; it
        // also publishes no country, so it always passes null there.
        var company = Company.Create("Acme", Now, website: "https://acme.example", industry: null, country: "TR");

        company.EnrichFrom("https://other.example", "Lojistik", country: null, Now);

        company.Website.ShouldBe("https://acme.example");
        company.Industry.ShouldBe("Lojistik");
        company.Country.ShouldBe("TR");
    }
}
