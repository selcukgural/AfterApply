using AfterApply.Application.JobSources;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class JobSourceQueryNormalizerTests
{
    [Fact]
    public void Same_Criteria_Differently_Typed_Share_A_Key()
    {
        var a = JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, ".NET   Developer", "İstanbul", JobSourceTimeWindow.Week, false);
        var b = JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, " .net developer ", "istanbul", JobSourceTimeWindow.Week, false);

        // Case and whitespace fold; diacritics do not (İ vs I is a different word to the source).
        JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, ".NET Developer", "İstanbul", JobSourceTimeWindow.Week, false).ShouldBe(a);
        a.ShouldNotBe(b);
        a.Length.ShouldBe(64);
    }

    [Fact]
    public void Every_Dimension_Is_Part_Of_The_Key()
    {
        var baseline = JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, "x", "y", JobSourceTimeWindow.Week, false);

        JobSourceQueryNormalizer.KeyHash(Source.KariyerNet, "x", "y", JobSourceTimeWindow.Week, false).ShouldNotBe(baseline);
        JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, "x", "y", JobSourceTimeWindow.Month, false).ShouldNotBe(baseline);
        JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, "x", "y", JobSourceTimeWindow.Week, true).ShouldNotBe(baseline);
        JobSourceQueryNormalizer.KeyHash(Source.LinkedIn, "x", "z", JobSourceTimeWindow.Week, false).ShouldNotBe(baseline);
    }

    [Fact]
    public void Collapse_Keeps_Case_And_Diacritics()
    {
        JobSourceQueryNormalizer.CollapseWhitespace("  Kıdemli\t\tYazılım   Mühendisi ").ShouldBe("Kıdemli Yazılım Mühendisi");
        JobSourceQueryNormalizer.NormalizeText("  Kıdemli Yazılım ").ShouldBe("kıdemli yazılım");
    }
}
