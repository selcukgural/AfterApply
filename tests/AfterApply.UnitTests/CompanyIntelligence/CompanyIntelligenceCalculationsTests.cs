using AfterApply.Application.CompanyIntelligence;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Domain.Companies;
using Shouldly;

namespace AfterApply.UnitTests.CompanyIntelligence;

public class CompanyIntelligenceCalculationsTests
{
    [Theory]
    [InlineData(0, ConfidenceBucket.Hidden)]
    [InlineData(49, ConfidenceBucket.Hidden)]
    [InlineData(50, ConfidenceBucket.VeryLow)]
    [InlineData(99, ConfidenceBucket.VeryLow)]
    [InlineData(100, ConfidenceBucket.Low)]
    [InlineData(249, ConfidenceBucket.Low)]
    [InlineData(250, ConfidenceBucket.Medium)]
    [InlineData(999, ConfidenceBucket.Medium)]
    [InlineData(1000, ConfidenceBucket.High)]
    [InlineData(50000, ConfidenceBucket.High)]
    public void ClassifyConfidence_With_Default_Thresholds_Returns_Expected_Bucket(int applicationCount, ConfidenceBucket expected)
    {
        var defaults = new CompanyIntelligenceOptions();

        CompanyIntelligenceCalculations
            .ClassifyConfidence(applicationCount, defaults.HiddenBelow, defaults.VeryLowBelow,
                defaults.LowBelow, defaults.MediumBelow)
            .ShouldBe(expected);
    }

    [Fact]
    public void The_Shipped_Ladder_Never_Names_A_Company_Below_Fifty_Applications()
    {
        // Deliberately asserts the shipped numbers, not just the function's behaviour. These are a
        // fairness decision, not a tuning knob: below this floor a percentage is one bad hiring
        // manager away from being a different percentage, and the company is named either way.
        // Raising these is safe and hides more; lowering them needs the fairness review, and this
        // test is what makes that a conversation rather than a quiet diff.
        var defaults = new CompanyIntelligenceOptions();

        defaults.HiddenBelow.ShouldBeGreaterThanOrEqualTo(50);
        defaults.Enabled.ShouldBeFalse();
        defaults.HiddenBelow.ShouldBeLessThan(defaults.VeryLowBelow);
        defaults.VeryLowBelow.ShouldBeLessThan(defaults.LowBelow);
        defaults.LowBelow.ShouldBeLessThan(defaults.MediumBelow);
    }

    [Fact]
    public void The_Window_Is_Bounded_So_An_Old_Reputation_Cannot_Be_Permanent()
    {
        // An unbounded aggregate gives a company no way to ever improve its number. Twelve months
        // is the shipped answer; what matters to this test is only that some finite window exists.
        var defaults = new CompanyIntelligenceOptions();

        defaults.WindowMonths.ShouldBeGreaterThan(0);
        defaults.WindowMonths.ShouldBeLessThanOrEqualTo(36);
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

    [Fact]
    public void CalculateResponseTimeScore_Returns_Null_When_No_Response_Data()
    {
        CompanyIntelligenceCalculations.CalculateResponseTimeScore(averageResponseTimeDays: null, capDays: 30)
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(0.0, 30, 100.0)] // instant response → full score
    [InlineData(15.0, 30, 50.0)] // halfway to the cap → half score
    [InlineData(30.0, 30, 0.0)] // exactly at the cap → 0
    [InlineData(60.0, 30, 0.0)] // past the cap → clamped to 0, not negative
    public void CalculateResponseTimeScore_Scales_Linearly_Down_To_The_Configured_Cap(
        double averageResponseTimeDays, int capDays, double expected)
    {
        CompanyIntelligenceCalculations.CalculateResponseTimeScore(averageResponseTimeDays, capDays)
            .ShouldBe(expected);
    }

    [Fact]
    public void CalculateCandidateExperienceScore_Averages_Three_Equally_Weighted_Sub_Scores()
    {
        // responsiveness=90, responseTimeScore=60, closureRate=30, equal weights → mean = 60
        CompanyIntelligenceCalculations
            .CalculateCandidateExperienceScore(
                responsiveness: 90, responseTimeScore: 60, closureRate: 30,
                responsivenessWeight: 1, responseTimeWeight: 1, closureRateWeight: 1)
            .ShouldBe(60.0);
    }

    [Fact]
    public void CalculateCandidateExperienceScore_Excludes_Null_ResponseTimeScore_Instead_Of_Treating_It_As_Zero()
    {
        // No response-time data: composite must be the mean of responsiveness(0) and
        // closureRate(0) alone (=0), not (0+0+0)/3 which would coincidentally also be 0 here —
        // the distinguishing case is below.
        CompanyIntelligenceCalculations
            .CalculateCandidateExperienceScore(
                responsiveness: 0, responseTimeScore: null, closureRate: 0,
                responsivenessWeight: 1, responseTimeWeight: 1, closureRateWeight: 1)
            .ShouldBe(0.0);

        // responsiveness=100, closureRate=100, no response-time data → mean of the two
        // available sub-scores is 100, not (100+0+100)/3=66.7 which is what treating the
        // missing sub-score as 0 would produce.
        CompanyIntelligenceCalculations
            .CalculateCandidateExperienceScore(
                responsiveness: 100, responseTimeScore: null, closureRate: 100,
                responsivenessWeight: 1, responseTimeWeight: 1, closureRateWeight: 1)
            .ShouldBe(100.0);
    }

    [Fact]
    public void CalculateCandidateExperienceScore_Honors_Custom_Weights_Not_Hardcoded_Equal_Split()
    {
        // Weight closureRate 3x — proves no hardcoded 1/3 split baked in, per DEVELOPMENT_PLAN.md
        // Sprint 11: weights are config-driven.
        CompanyIntelligenceCalculations
            .CalculateCandidateExperienceScore(
                responsiveness: 0, responseTimeScore: 0, closureRate: 100,
                responsivenessWeight: 1, responseTimeWeight: 1, closureRateWeight: 3)
            .ShouldBe(60.0); // (0*1 + 0*1 + 100*3) / 5
    }
}
