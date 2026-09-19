using AfterApply.Domain.Blog;
using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogSlugGeneratorTests
{
    [Theory]
    [InlineData("İşe Alım Sürecinde Ghosting", "ise-alim-surecinde-ghosting")]
    [InlineData("CV'nizi ATS için nasıl hazırlarsınız?", "cv-nizi-ats-icin-nasil-hazirlarsiniz")]
    [InlineData("Çok   boşluk — ve noktalama!!", "cok-bosluk-ve-noktalama")]
    [InlineData("Why 80% of applications get no answer", "why-80-of-applications-get-no-answer")]
    public void Folds_Turkish_Letters_And_Punctuation(string title, string expected)
    {
        BlogSlugGenerator.Generate(title).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Türk Telekom")]
    [InlineData("İş Bankası")]
    [InlineData("Işık Üniversitesi")]
    [InlineData("Ağaç & Orman Ltd. Şti.")]
    public void Folds_The_Same_Letters_As_The_Company_Generator(string text)
    {
        // A copy, not a reference (module isolation) — this is what keeps the two in step.
        BlogSlugGenerator.Generate(text).ShouldBe(CompanySlugGenerator.Generate(text));
    }

    [Fact]
    public void A_Title_With_No_Usable_Character_Falls_Back()
    {
        BlogSlugGenerator.Generate("!!! ???").ShouldBe(BlogSlugGenerator.Fallback);
    }

    [Theory]
    [InlineData("Media")]
    [InlineData("new")]
    [InlineData("preview")]
    [InlineData("Public")]
    public void A_Reserved_Segment_Is_Suffixed_Rather_Than_Taken(string title)
    {
        BlogSlugGenerator.Generate(title).ShouldEndWith("-2");
        BlogSlugGenerator.Reserved.ShouldContain(BlogSlugGenerator.Generate(title)[..^2]);
    }

    [Fact]
    public void Long_Titles_Are_Cut_Without_A_Dangling_Hyphen()
    {
        var title = string.Join(' ', Enumerable.Repeat("word", 60));
        var slug = BlogSlugGenerator.Generate(title);

        slug.Length.ShouldBeLessThanOrEqualTo(BlogSlugGenerator.MaxLength);
        slug.ShouldNotEndWith("-");
    }

    [Theory]
    [InlineData("ise-alim", true)]
    [InlineData("a", true)]
    [InlineData("2026-in-review", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Ise-Alim", false)]
    [InlineData("ise--alim", false)]
    [InlineData("-ise", false)]
    [InlineData("ise alim", false)]
    [InlineData("işe", false)]
    [InlineData("media", false)]
    public void Validates_Hand_Typed_Slugs(string? slug, bool expected)
    {
        BlogSlugGenerator.IsValid(slug).ShouldBe(expected);
    }

    [Fact]
    public void A_Generated_Slug_Is_Always_Valid()
    {
        foreach (var title in new[] { "İşe Alım", "!!!", "Media", string.Join(' ', Enumerable.Repeat("word", 60)) })
        {
            BlogSlugGenerator.IsValid(BlogSlugGenerator.Generate(title)).ShouldBeTrue(title);
        }
    }
}
