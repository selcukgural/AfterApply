using AfterApply.Domain.Occupations;
using Shouldly;

namespace AfterApply.UnitTests.Occupations;

public class OccupationNameTests
{
    [Theory]
    [InlineData("Yazılım geliştiricileri", "YAZILIM GELIŞTIRICILERI")]
    [InlineData("YAZILIM GELİŞTİRİCİLERİ", "YAZILIM GELIŞTIRICILERI")]
    [InlineData(" yazilim   geliştiricileri ", "YAZILIM GELIŞTIRICILERI")]
    [InlineData("Backend\tDeveloper", "BACKEND DEVELOPER")]
    public void Folds_Turkish_I_Variants_And_Whitespace(string input, string expected)
    {
        OccupationName.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Ids_Are_Derived_From_The_Code_And_Stable()
    {
        Occupation.IdFor("2512").ShouldBe(Occupation.IdFor(" 2512 "));
        Occupation.IdFor("2512").ShouldNotBe(Occupation.IdFor("2513"));
        // Pinned: a changed namespace or hashing would re-key every seeded row.
        Occupation.IdFor("1111").ShouldBe(Guid.Parse("13ce36ea-20e1-5553-bd4d-1ecfcd204f8c"));
    }

    [Fact]
    public void Create_Normalises_Both_Names()
    {
        var o = Occupation.Create("2512", "2512", " Yazılım geliştiricileri ", "Software Developers ", OccupationSource.Isco08);

        o.Id.ShouldBe(Occupation.IdFor("2512"));
        o.NameTr.ShouldBe("Yazılım geliştiricileri");
        o.NormalizedNameTr.ShouldBe("YAZILIM GELIŞTIRICILERI");
        o.NormalizedNameEn.ShouldBe("SOFTWARE DEVELOPERS");
        o.IsActive.ShouldBeTrue();
        o.Retire();
        o.IsActive.ShouldBeFalse();
    }
}
