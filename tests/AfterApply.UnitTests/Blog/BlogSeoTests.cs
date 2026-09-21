using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogSeoTests
{
    [Fact]
    public void Normalize_Trims_Blanks_To_Null_And_Dedupes_Keywords_Case_Folded()
    {
        var seo = BlogSeo.Normalize("  İşe Alımda Ghosting ", "   ", [" İK geri dönüş ", "ik geri dönüş", "", "mülakat"], null);

        seo.SeoTitle.ShouldBe("İşe Alımda Ghosting");
        seo.PrimaryKeyword.ShouldBeNull();
        seo.SecondaryKeywords.ShouldBe(["İK geri dönüş", "mülakat"]);
        seo.CoverAlt.ShouldBeNull();
        seo.AllKeywords.ShouldBe(["İK geri dönüş", "mülakat"]);
    }

    [Fact]
    public void Normalize_Of_Nothing_Is_Empty()
    {
        BlogSeo.Normalize(null, null, null, null).ShouldBe(BlogSeo.Empty);
        BlogSeo.Empty.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validity_Is_The_Store_Caps()
    {
        new BlogSeo(null, null, Enumerable.Repeat("k", BlogSeo.MaxSecondaryKeywords + 1).ToArray(), null).IsValid.ShouldBeFalse();
        new BlogSeo(null, null, [new string('k', BlogSeo.MaxKeywordLength + 1)], null).IsValid.ShouldBeFalse();
        new BlogSeo(null, new string('k', BlogSeo.MaxKeywordLength), [], new string('a', BlogSeo.MaxCoverAltLength)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void All_Keywords_Puts_The_Primary_First()
    {
        new BlogSeo(null, "ghosting", ["sessizlik"], null).AllKeywords.ShouldBe(["ghosting", "sessizlik"]);
    }
}
