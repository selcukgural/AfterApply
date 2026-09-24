using AfterApply.Application.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

/// <summary>A profile page is trusted only when it names the company (2026-09-24). Company names
/// arrive from any job site, so the same firm is spelled several ways; a page naming another firm
/// must still be refused.</summary>
public class CompanyPageIdentityTests
{
    [Theory]
    [InlineData("Microsoft", "Microsoft")]
    [InlineData("TRENDYOL TEKNOLOJİ A.Ş.", "Trendyol Teknoloji")]
    [InlineData("Trendyol", "Trendyol Group")]
    [InlineData("Trendyol | Careers", "Trendyol")]
    [InlineData("Coca-Cola", "Coca Cola")]
    [InlineData("Borusan Lojistik", "Borusan Lojistik Dağıtım Depolama Taşımacılık")]
    [InlineData("İş Bankası", "IŞ BANKASI")]
    [InlineData("HP", "HP")]
    public void Accepts_The_Same_Company_Spelled_Differently(string company, string page) =>
        CompanyPageIdentity.Matches(company, page).ShouldBeTrue();

    [Theory]
    [InlineData("Trendyol", "Evil Corp")]
    [InlineData("Trendyol", "Hepsiburada")]
    [InlineData("Trendyol Teknoloji", "Trendyol Express")]
    [InlineData("Garanti BBVA", "Garanti Phishing Services Ltd")]
    public void Refuses_A_Page_That_Names_Another_Company(string company, string page) =>
        CompanyPageIdentity.Matches(company, page).ShouldBeFalse();

    [Fact]
    public void A_Very_Short_Name_Must_Match_Exactly()
    {
        // "HP" as the first word of a longer page name says little about which firm it is.
        CompanyPageIdentity.Matches("HP", "HP Sahte Sayfa").ShouldBeFalse();
        CompanyPageIdentity.Matches("HP", "HP").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("|||")]
    public void Refuses_A_Page_That_Names_No_Company(string? page) =>
        CompanyPageIdentity.Matches("Trendyol", page).ShouldBeFalse();
}
