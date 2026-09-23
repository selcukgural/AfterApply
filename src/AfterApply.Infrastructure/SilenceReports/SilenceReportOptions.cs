namespace AfterApply.Infrastructure.SilenceReports;

public sealed class SilenceReportOptions
{
    public const string SectionName = "SilenceReports";

    /// <summary>Whether company pages offer the form and accept reports. Collecting publishes
    /// nothing — what a reader sees still sits behind <c>CompanyIntelligence:Enabled</c> — so this
    /// is on by default.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Reports a company needs in the window before its count is shown. Five, from a form that
    /// cannot tell two people apart, is deliberately a floor for "more than a grudge", not a
    /// sample size; the quarter rule below does the rest. Raising it only withholds more;
    /// lowering it is a publishing decision.
    /// </summary>
    public int MinimumReports { get; init; } = 5;

    /// <summary>Distinct calendar quarters the silences must have begun in. A burst from one
    /// hiring round, or from one person with a grievance, lands in one quarter.</summary>
    public int MinimumQuarters { get; init; } = 2;

    /// <summary>How far back a report counts toward the displayed figure, in months — the same
    /// twelve as the company's other figures. Older rows stay stored; they are only not shown.</summary>
    public int WindowMonths { get; init; } = 12;

    /// <summary>How long one connection must wait before reporting the same company again. Held
    /// in Redis as a keyed hash with this TTL; nothing about it reaches the database.</summary>
    public int RepeatDays { get; init; } = 30;
}
