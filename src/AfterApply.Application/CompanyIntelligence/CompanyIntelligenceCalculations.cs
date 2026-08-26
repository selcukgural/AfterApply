using AfterApply.Domain.Companies;

namespace AfterApply.Application.CompanyIntelligence;

public static class CompanyIntelligenceCalculations
{
    public static ConfidenceBucket ClassifyConfidence(
        int applicationCount, int hiddenBelow, int veryLowBelow, int lowBelow, int mediumBelow)
    {
        if (applicationCount < hiddenBelow)
        {
            return ConfidenceBucket.Hidden;
        }

        if (applicationCount < veryLowBelow)
        {
            return ConfidenceBucket.VeryLow;
        }

        if (applicationCount < lowBelow)
        {
            return ConfidenceBucket.Low;
        }

        if (applicationCount < mediumBelow)
        {
            return ConfidenceBucket.Medium;
        }

        return ConfidenceBucket.High;
    }

    // Null when there is no response-time data at all (no application from this company has ever
    // been responded to) — the caller must not treat that the same as a 0 score, since 0 would
    // mean "responded, but as late as possible," which is a different and more forgiving claim
    // than "never responded." CalculateCandidateExperienceScore drops a null sub-score from the
    // weighted average instead of scoring it 0.
    public static double? CalculateResponseTimeScore(double? averageResponseTimeDays, int capDays)
    {
        if (averageResponseTimeDays is null)
        {
            return null;
        }

        var fraction = 1.0 - averageResponseTimeDays.Value / capDays;
        return Math.Round(100.0 * Math.Clamp(fraction, 0.0, 1.0), 1);
    }

    // Weighted average over whichever of the three sub-scores are actually available.
    // responseTimeScore is the only one that can be null (see CalculateResponseTimeScore) — when
    // it is, its weight is excluded from the denominator rather than treated as a 0 contribution,
    // so a company with zero responses isn't scored as if it responded instantly.
    public static double CalculateCandidateExperienceScore(
        double responsiveness, double? responseTimeScore, double closureRate,
        double responsivenessWeight, double responseTimeWeight, double closureRateWeight)
    {
        var weightedSum = responsiveness * responsivenessWeight + closureRate * closureRateWeight;
        var totalWeight = responsivenessWeight + closureRateWeight;

        if (responseTimeScore is not null)
        {
            weightedSum += responseTimeScore.Value * responseTimeWeight;
            totalWeight += responseTimeWeight;
        }

        return totalWeight <= 0 ? 0 : Math.Round(weightedSum / totalWeight, 1);
    }
}
