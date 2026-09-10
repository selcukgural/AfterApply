using AfterApply.Application.CvScan;
using Shouldly;

namespace AfterApply.UnitTests.CvScan;

/// <summary>
/// The score's contract, which the page's copy leans on in three places: the headline is the sum of
/// the four category subtotals, a fix list adds up to what the fixes are worth, and no category can
/// go negative. All three are stated here rather than trusted.
/// </summary>
public class CvScanScoringTests
{
    private static CvScanFindingCandidate Candidate(CvScanFindingCode code, CvScanCategory category, int cost) =>
        new(code, category, cost, [new CvScanEvidence(1, "line")], new Dictionary<string, double>());

    [Fact]
    public void Weights_Should_Sum_To_The_Maximum_Score()
    {
        CvScanScoring.Weights.Values.Sum().ShouldBe(CvScanScoring.MaximumScore);
    }

    [Fact]
    public void A_Clean_Cv_Should_Score_The_Maximum()
    {
        var result = CvScanScoring.Score([]);

        result.Score.ShouldBe(100);
        result.Findings.ShouldBeEmpty();
        result.Categories.ShouldAllBe(category => category.Score == category.Weight);
    }

    [Fact]
    public void Headline_Should_Equal_The_Sum_Of_The_Category_Subtotals()
    {
        var result = CvScanScoring.Score([
            Candidate(CvScanFindingCode.MultiColumnOrTableLayout, CvScanCategory.MachineReadability, 14),
            Candidate(CvScanFindingCode.SectionsOrDatesUnreadable, CvScanCategory.SectionsAndDates, 10),
            Candidate(CvScanFindingCode.ContactUnreadable, CvScanCategory.Contact, 15),
            Candidate(CvScanFindingCode.LengthOutOfRange, CvScanCategory.FormatAndLength, 8)
        ]);

        result.Categories.Sum(category => category.Score).ShouldBe(result.Score);
        result.Score.ShouldBe(100 - (14 + 10 + 15 + 8));
    }

    /// <summary>
    /// The arithmetic the result page prints ("three fixes, 32 points") has to be a subtraction the
    /// reader can check, not a promise — so the costs shown must be the costs charged.
    /// </summary>
    [Fact]
    public void Finding_Costs_Should_Account_For_Every_Point_Lost()
    {
        var result = CvScanScoring.Score([
            Candidate(CvScanFindingCode.NoTextLayer, CvScanCategory.MachineReadability, 40),
            Candidate(CvScanFindingCode.BrokenTurkishCharacters, CvScanCategory.MachineReadability, 12),
            Candidate(CvScanFindingCode.SectionsOrDatesUnreadable, CvScanCategory.SectionsAndDates, 25),
            Candidate(CvScanFindingCode.ContactUnreadable, CvScanCategory.Contact, 15),
            Candidate(CvScanFindingCode.LengthOutOfRange, CvScanCategory.FormatAndLength, 12),
            Candidate(CvScanFindingCode.InconsistentFormatting, CvScanCategory.FormatAndLength, 8)
        ]);

        result.Findings.Sum(finding => finding.PointCost).ShouldBe(100 - result.Score);
    }

    [Fact]
    public void A_Category_Should_Never_Go_Negative()
    {
        var result = CvScanScoring.Score([
            Candidate(CvScanFindingCode.NoTextLayer, CvScanCategory.MachineReadability, 40),
            Candidate(CvScanFindingCode.BrokenTurkishCharacters, CvScanCategory.MachineReadability, 12),
            Candidate(CvScanFindingCode.MultiColumnOrTableLayout, CvScanCategory.MachineReadability, 14)
        ]);

        var machineReadability = result.Categories
            .Single(category => category.Category == CvScanCategory.MachineReadability);

        machineReadability.Score.ShouldBe(0);
        result.Findings.Sum(finding => finding.PointCost).ShouldBe(40);
    }

    /// <summary>Severity is paid first, so the finding that gets clamped to nothing is the cheaper
    /// one — a reader should not see the worst problem on their CV listed as free to fix.</summary>
    [Fact]
    public void The_Severe_Finding_Should_Be_Charged_Before_The_Cheap_One()
    {
        var result = CvScanScoring.Score([
            Candidate(CvScanFindingCode.BrokenTurkishCharacters, CvScanCategory.MachineReadability, 12),
            Candidate(CvScanFindingCode.NoTextLayer, CvScanCategory.MachineReadability, 40)
        ]);

        result.Findings.First(finding => finding.Code == CvScanFindingCode.NoTextLayer).PointCost.ShouldBe(40);
        result.Findings.First(finding => finding.Code == CvScanFindingCode.BrokenTurkishCharacters)
            .PointCost.ShouldBe(0);
    }

    [Fact]
    public void A_Finding_With_Nothing_To_Point_At_Should_Be_Dropped()
    {
        var result = CvScanScoring.Score([
            new CvScanFindingCandidate(CvScanFindingCode.LengthOutOfRange, CvScanCategory.FormatAndLength, 12,
                [new CvScanEvidence(null, "   ")], new Dictionary<string, double>())
        ]);

        result.Findings.ShouldBeEmpty();
        result.Score.ShouldBe(100);
    }
}
