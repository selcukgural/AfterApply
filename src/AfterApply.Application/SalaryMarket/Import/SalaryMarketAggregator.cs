namespace AfterApply.Application.SalaryMarket.Import;

/// <summary>
/// Turns normalised answers into the published cells: for every year and occupation, the whole
/// group, each level and each experience range, with every source of that year pooled into one
/// (DECISIONS.md 2026-09-27). A slice with fewer than <c>minimum</c> answers is not a cell at all
/// — it is never written, so no later reader can publish it by mistake.
/// </summary>
public static class SalaryMarketAggregator
{
    /// <summary>Figures are rounded to the nearest 500 TL: the answers are ranges several
    /// thousand wide, and "132.500" claims no precision the survey did not have.</summary>
    public const int RoundTo = 500;

    public static IReadOnlyList<SalaryMarketCell> Aggregate(IEnumerable<NormalizedResponse> responses, int minimum)
    {
        var cells = new List<SalaryMarketCell>();
        foreach (var slice in responses.GroupBy(r => (r.Year, r.Group)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Group, StringComparer.Ordinal))
        {
            var (year, group) = slice.Key;
            var answers = slice.ToList();
            Add(cells, year, group, SalaryMarketDimension.All, string.Empty, answers, minimum);

            foreach (var level in Enum.GetValues<SalaryMarketLevel>())
            {
                Add(cells, year, group, SalaryMarketDimension.Level, level.ToString(),
                    answers.Where(a => a.Level == level).ToList(), minimum);
            }

            foreach (var experience in Enum.GetValues<SalaryMarketExperience>())
            {
                Add(cells, year, group, SalaryMarketDimension.Experience, experience.ToString(),
                    answers.Where(a => a.Experience == experience).ToList(), minimum);
            }
        }

        return cells;
    }

    private static void Add(List<SalaryMarketCell> cells, int year, string group, SalaryMarketDimension dimension,
        string bucket, IReadOnlyList<NormalizedResponse> answers, int minimum)
    {
        if (answers.Count >= minimum)
        {
            cells.Add(new SalaryMarketCell(year, group, dimension, bucket, Stats(answers.Select(a => a.Salary).ToList())));
        }
    }

    /// <summary>Nearest-rank percentiles over the answers' values. An open-top answer counts
    /// at its floor, and a percentile that lands on one is flagged "at least".</summary>
    public static SalaryStats Stats(IReadOnlyList<SalaryBand> bands)
    {
        if (bands.Count == 0)
        {
            throw new ArgumentException("A cell needs at least one answer.", nameof(bands));
        }

        var sorted = bands.OrderBy(b => b.Value).ThenBy(b => b.OpenTop).ToList();
        var atLeast = SalaryPercentiles.None;

        int At(double p, SalaryPercentiles flag)
        {
            var index = (int)Math.Round(p * (sorted.Count - 1), MidpointRounding.AwayFromZero);
            var band = sorted[index];
            if (band.OpenTop)
            {
                atLeast |= flag;
            }

            return (int)(Math.Round(band.Value / RoundTo, MidpointRounding.AwayFromZero) * RoundTo);
        }

        var p10 = At(0.10, SalaryPercentiles.P10);
        var p25 = At(0.25, SalaryPercentiles.P25);
        var p50 = At(0.50, SalaryPercentiles.P50);
        var p75 = At(0.75, SalaryPercentiles.P75);
        var p90 = At(0.90, SalaryPercentiles.P90);
        return new SalaryStats(sorted.Count, p10, p25, p50, p75, p90, atLeast);
    }
}
