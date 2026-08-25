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
}
