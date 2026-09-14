namespace AfterApply.Infrastructure.Auditing;

/// <summary>Bound from the <c>RequestAudit</c> section.</summary>
public sealed class RequestAuditOptions
{
    public const string SectionName = "RequestAudit";

    /// <summary>
    /// How long a row with no account behind it (anonymous CV scan, benchmark answer, failed
    /// sign-in) is kept before the purge job deletes it. Twelve months: inside the one-to-two-year
    /// band Turkish law (5651) expects hosting providers to keep traffic records, and the figure
    /// the privacy policy quotes — change both together.
    /// </summary>
    public int AnonymousRetentionDays { get; init; } = 365;

    /// <summary>Once a night, off-peak; the purge is a single range delete.</summary>
    public string PurgeCronExpression { get; init; } = "0 4 * * *";
}
