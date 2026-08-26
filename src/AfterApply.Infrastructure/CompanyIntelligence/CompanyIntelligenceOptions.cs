namespace AfterApply.Infrastructure.CompanyIntelligence;

public sealed class CompanyIntelligenceOptions
{
    public bool Enabled { get; init; } = false;

    public int HiddenBelow { get; init; } = 20;

    public int VeryLowBelow { get; init; } = 50;

    public int LowBelow { get; init; } = 200;

    public int MediumBelow { get; init; } = 1000;

    // Average response time (days) at/beyond which the Response Time sub-score bottoms out at 0.
    // Own field rather than reusing Notifications:GhostingThresholdDays — same default value by
    // coincidence, not by shared meaning: that one flags "possibly ghosted," this one caps a
    // score curve. See DECISIONS.md Sprint 11 entry.
    public int ResponseTimeCapDays { get; init; } = 30;

    // Candidate Experience Score sub-metric weights — relative, not required to sum to 1;
    // CalculateCandidateExperienceScore normalizes by whichever weights are actually in play.
    // Equal by default; spec §14 gives no concrete weighting formula.
    public double ResponsivenessWeight { get; init; } = 1.0;

    public double ResponseTimeWeight { get; init; } = 1.0;

    public double ClosureRateWeight { get; init; } = 1.0;
}
