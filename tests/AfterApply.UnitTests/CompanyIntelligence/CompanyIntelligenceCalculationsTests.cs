using AfterApply.Application.CompanyIntelligence;
using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.CompanyIntelligence;

public class CompanyIntelligenceCalculationsTests
{
    [Theory]
    [InlineData(0, ConfidenceBucket.Hidden)]
    [InlineData(19, ConfidenceBucket.Hidden)]
    [InlineData(20, ConfidenceBucket.VeryLow)]
    [InlineData(49, ConfidenceBucket.VeryLow)]
    [InlineData(50, ConfidenceBucket.Low)]
    [InlineData(199, ConfidenceBucket.Low)]
    [InlineData(200, ConfidenceBucket.Medium)]
    [InlineData(999, ConfidenceBucket.Medium)]
    [InlineData(1000, ConfidenceBucket.High)]
    [InlineData(50000, ConfidenceBucket.High)]
    public void ClassifyConfidence_With_Default_Thresholds_Returns_Expected_Bucket(int applicationCount, ConfidenceBucket expected)
    {
        CompanyIntelligenceCalculations
            .ClassifyConfidence(applicationCount, hiddenBelow: 20, veryLowBelow: 50, lowBelow: 200, mediumBelow: 1000)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, ConfidenceBucket.Hidden)]
    [InlineData(5, ConfidenceBucket.VeryLow)]
    [InlineData(9, ConfidenceBucket.VeryLow)]
    [InlineData(10, ConfidenceBucket.Low)]
    public void ClassifyConfidence_Honors_Custom_Thresholds_Not_Hardcoded_Defaults(int applicationCount, ConfidenceBucket expected)
    {
        // Thresholds far from the spec's starting hypothesis (5/10/20/40) — proves the function
        // has no hardcoded numbers baked in, per spec §15: "Bu eşikler ileride gerçek data ile
        // değiştirilebilir."
        CompanyIntelligenceCalculations
            .ClassifyConfidence(applicationCount, hiddenBelow: 5, veryLowBelow: 10, lowBelow: 20, mediumBelow: 40)
            .ShouldBe(expected);
    }
}
