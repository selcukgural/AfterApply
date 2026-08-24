using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class CompanyNameNormalizerTests
{
    [Theory]
    [InlineData("Acme Yazılım A.Ş.", "ACME YAZILIM")]
    [InlineData("  Acme   Yazılım  ", "ACME YAZILIM")]
    [InlineData("Acme Inc.", "ACME")]
    [InlineData("Acme LLC", "ACME")]
    public void Normalize_Strips_Whitespace_Case_And_Legal_Suffixes(string input, string expected)
    {
        CompanyNameNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_Folds_Turkish_Dotted_And_Dotless_I_Together()
    {
        // .NET's invariant culture does not round-trip ı/İ the way Turkish
        // culture does, so this needs explicit folding (TurkishTextNormalizer) —
        // otherwise "Yazılım" and "YAZILIM" would normalize to different keys.
        var withDotlessI = CompanyNameNormalizer.Normalize("Yazılım");
        var withAsciiI = CompanyNameNormalizer.Normalize("YAZILIM");

        withDotlessI.ShouldBe(withAsciiI);
    }

    [Fact]
    public void Normalize_Does_Not_Merge_Textual_Synonyms()
    {
        // Known limitation (logged in DECISIONS.md): case/whitespace/suffix
        // normalization only — "ABC Teknoloji" and "ABC Tech" are semantically
        // the same company but textually different, so they stay distinct.
        var first = CompanyNameNormalizer.Normalize("ABC Teknoloji");
        var second = CompanyNameNormalizer.Normalize("ABC Tech");

        first.ShouldNotBe(second);
    }
}
