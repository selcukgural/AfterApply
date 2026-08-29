using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class KariyerNetJobDescriptionParserTests
{
    [Fact]
    public void Parses_Title_And_Company_From_Real_Template()
    {
        var (title, company) = KariyerNetJobDescriptionParser.Parse(
            "Kariyer.net'teki CCN Yatırım Holding A.Ş. firmasına ait Kıdemli Yazılım Geliştirme Uzmanı iş ilanını hemen inceleyin ve başvurun!");

        title.ShouldBe("Kıdemli Yazılım Geliştirme Uzmanı");
        company.ShouldBe("CCN Yatırım Holding A.Ş.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bambaşka bir açıklama metni.")]
    public void Returns_Null_For_Missing_Or_NonMatching_Description(string? input)
    {
        var (title, company) = KariyerNetJobDescriptionParser.Parse(input);

        title.ShouldBeNull();
        company.ShouldBeNull();
    }
}
