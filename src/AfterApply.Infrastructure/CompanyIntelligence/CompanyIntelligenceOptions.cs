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
    /// The recency distortion this used to carry — applications too young to have been answered
    /// dragging the rates down — is settled by <see cref="MaturityDays"/> (2026-09-22).
    /// </summary>
    public int WindowMonths { get; init; } = 12;

    /// <summary>
    /// An application younger than this is not judged: it stays in the window's count (and so in
    /// the confidence ladder) but out of every rate's denominator, because "no reply yet" three
    /// days in is not "no reply". Thirty days is the same figure as the ghosting reminder's
    /// threshold and means the same thing here — the point at which silence starts to be an
    /// answer — but it is its own setting so the two can move apart if the data says they should.
    /// </summary>
    public int MaturityDays { get; init; } = 30;

    /// <summary>
    /// The largest share (0–1) of a company's applications one person may account for before the
    /// page hides itself. A "company aggregate" that is mostly one person's history is that
    /// person's job search with a company name on it — fifty applications from two people is
    /// not fifty opinions. A third: at the fifty-application floor that forces at least three
    /// contributors, and no one of them is the number. Hidden by this rule looks exactly like
    /// Hidden by the count, on purpose.
    /// </summary>
    public double MaxContributorShare { get; init; } = 1.0 / 3.0;

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
