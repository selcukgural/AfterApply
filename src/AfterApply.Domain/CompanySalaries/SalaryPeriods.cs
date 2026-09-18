namespace AfterApply.Domain.CompanySalaries;

/// <summary>
/// Which salary rows still describe the company today. A row is current when its period is known
/// and either still open (a current employee's) or ended no earlier than <c>cutoffYear</c>; the
/// company page lists the rest under "previous periods" and leaves them out of the median. A row
/// whose period was never given is not current: nobody knows when it was drawn.
/// </summary>
public static class SalaryPeriods
{
    /// <summary>The first year that still counts as current: a two-year window in 2026 keeps
    /// 2025 and 2026, so the cutoff is 2025.</summary>
    public static int CutoffYear(int currentYear, int windowYears) => currentYear - Math.Max(windowYears, 1) + 1;

    /// <summary>The same test the list query writes inline (EF cannot translate a call), so a
    /// unit test and the in-memory projection agree with the SQL.</summary>
    public static bool IsCurrent(int? periodStartYear, int? periodEndYear, int cutoffYear) =>
        periodStartYear is not null && (periodEndYear is null || periodEndYear >= cutoffYear);
}
