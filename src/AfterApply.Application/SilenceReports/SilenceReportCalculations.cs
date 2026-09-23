using AfterApply.Domain.SilenceReports;

namespace AfterApply.Application.SilenceReports;

public static class SilenceReportCalculations
{
    /// <summary>
    /// A company's reports in the window, or null below the floor. The floor is two-part: at
    /// least <paramref name="minimumReports"/> reports, and reports whose silence began in at
    /// least <paramref name="minimumQuarters"/> different calendar quarters. The second half is
    /// what one determined person, or one bad week at one company, cannot fake with volume — a
    /// burst from a single hiring round lands in a single quarter.
    /// </summary>
    public static CompanySilenceReports? Summarize(
        IReadOnlyCollection<(SilenceStage Stage, DateOnly SilentSinceMonth)> reports,
        int minimumReports, int minimumQuarters)
    {
        if (reports.Count < minimumReports)
        {
            return null;
        }

        var quarters = reports.Select(r => QuarterOf(r.SilentSinceMonth)).Distinct().Count();
        if (quarters < minimumQuarters)
        {
            return null;
        }

        var byStage = reports
            .GroupBy(r => r.Stage)
            .Select(g => new SilenceStageCount(g.Key, g.Count()))
            .OrderBy(s => s.Stage)
            .ToList();

        return new CompanySilenceReports(reports.Count, byStage);
    }

    public static (int Year, int Quarter) QuarterOf(DateOnly month) => (month.Year, (month.Month - 1) / 3 + 1);
}
