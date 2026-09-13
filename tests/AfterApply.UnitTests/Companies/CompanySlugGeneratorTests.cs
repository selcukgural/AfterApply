using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Companies;

public class CompanySlugGeneratorTests
{
    // These pairs are also what the AddCompanyReviews migration's SQL backfill has to produce —
    // the C# generator and the SQL translate() table are kept in step through this list.
    [Theory]
    [InlineData("Türk Telekom", "turk-telekom")]
    [InlineData("TÜRK HAVA YOLLARI A.Ş.", "turk-hava-yollari-a-s")]
    [InlineData("İş Bankası", "is-bankasi")]
    [InlineData("Işık Üniversitesi", "isik-universitesi")]
    [InlineData("Çiçek Sepeti", "cicek-sepeti")]
    [InlineData("Acme Inc.", "acme-inc")]
    [InlineData("  Multiple   spaces -- and punctuation!!  ", "multiple-spaces-and-punctuation")]
    [InlineData("Ağaç & Orman Ltd. Şti.", "agac-orman-ltd-sti")]
    public void Folds_Turkish_Letters_And_Punctuation(string name, string expected)
    {
        CompanySlugGenerator.Generate(name).ShouldBe(expected);
    }

    [Fact]
    public void A_Name_With_No_Usable_Character_Falls_Back()
    {
        CompanySlugGenerator.Generate("!!! ???").ShouldBe(CompanySlugGenerator.Fallback);
    }

    [Fact]
    public void A_Reserved_Segment_Is_Suffixed_Rather_Than_Taken()
    {
        // "/companies/scoring" is a page, not a company.
        CompanySlugGenerator.Generate("Scoring").ShouldBe("scoring-2");
    }

    [Fact]
    public void Long_Names_Are_Cut_Without_A_Dangling_Hyphen()
    {
        var name = string.Join(' ', Enumerable.Repeat("word", 40));

        var slug = CompanySlugGenerator.Generate(name);

        slug.Length.ShouldBeLessThanOrEqualTo(CompanySlugGenerator.MaxLength);
        slug.ShouldNotEndWith("-");
    }

    [Fact]
    public void WithSuffix_Is_The_Collision_Format()
    {
        CompanySlugGenerator.WithSuffix("acme", 2).ShouldBe("acme-2");
    }

    [Fact]
    public void Company_Create_Assigns_A_Slug_By_Default()
    {
        var company = Company.Create("Türk Telekom", DateTimeOffset.UtcNow);

        company.Slug.ShouldBe("turk-telekom");
    }

    [Fact]
    public void AssignSlug_Never_Replaces_An_Existing_One()
    {
        // A slug is a published URL.
        var company = Company.Create("Acme", DateTimeOffset.UtcNow, slug: "acme-2");

        company.AssignSlug("acme", DateTimeOffset.UtcNow);

        company.Slug.ShouldBe("acme-2");
    }
}
