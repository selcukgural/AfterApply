namespace AfterApply.Infrastructure.CompanyIntelligence;

public sealed class CompanyIntelligenceOptions
{
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// How far back an application counts, in months. Metrics used to be computed over all time,
    /// which is the least fair aggregate there is: a company that ghosted everyone two years ago
    /// and has since fixed its process would carry the old number forever, and there would be no
    /// way for it to ever improve. A stated window means the figure describes the company as it is
    /// now, and the response says which window it used.
    ///
    /// <b>Known limitation, to settle before anything is published under a company's name:</b>
    /// applications submitted very recently have not had time to be answered, so they push the
    /// response rate down and the ghosting rate up. The distortion is small at twelve months and
    /// would not be at one; the honest fix is to exclude applications younger than the ghosting
    /// threshold from those two denominators, which is a change to what the numbers mean and
    /// belongs with the fairness review, not here.
    /// </summary>
    public int WindowMonths { get; init; } = 12;

    /// <summary>
    /// The confidence ladder, raised (2026-09-07) from 20/50/200/1000. Twenty applications is not a
    /// sample you can name a company on: it is a handful of people, plausibly from one team or one
    /// month, and a single bad hiring manager moves every percentage several points. Fifty is still
    /// not much — it is the floor at which the number stops being an anecdote, which is why nothing
    /// at all is exposed below it.
    ///
    /// Raising this only ever hides more, never less, so it is safe to raise again after seeing
    /// real distributions and unsafe to lower without one.
    /// </summary>
    public int HiddenBelow { get; init; } = 50;

    public int VeryLowBelow { get; init; } = 100;

    public int LowBelow { get; init; } = 250;

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
