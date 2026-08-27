using AfterApply.Application.Imports;
using Shouldly;

namespace AfterApply.UnitTests.Imports;

public class KariyerNetJobIdExtractorTests
{
    [Theory]
    [InlineData("https://www.kariyer.net/is-ilani/dolusoft-yazilim-teknolojileri-limited-sirketi-yazilim-destek-uzmani-4539310", "4539310")]
    [InlineData("https://www.kariyer.net/is-ilani/dolusoft-yazilim-teknolojileri-limited-sirketi-yazilim-destek-uzmani-4539310/", "4539310")]
    [InlineData("https://www.kariyer.net/is-ilani/erc-soft-bilgi-teknolojileri-dan-ltd-sti-yazilim-destek-uzmani-2630235?originSection=srp", "2630235")]
    [InlineData("https://www.kariyer.net/is-ilani/ankara-bilgi-teknolojileri-reklamcilik-turizm-sana-kidemli-yazilim-gelistirme-uzmani-4523032", "4523032")]
    public void Extract_Returns_Numeric_Id_From_KariyerNet_Job_Url(string url, string expected)
    {
        KariyerNetJobIdExtractor.Extract(url).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com/careers/backend-engineer")]
    [InlineData("https://www.kariyer.net/is-ilanlari/ankara-yazilim+destek+uzmani")]
    public void Extract_Returns_Null_When_No_Id_Present(string? url)
    {
        KariyerNetJobIdExtractor.Extract(url).ShouldBeNull();
    }
}
