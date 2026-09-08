namespace AfterApply.Infrastructure.Benchmark;

public sealed class BenchmarkOptions
{
    public const string SectionName = "Benchmark";

    /// <summary>
    /// How many answers a sector needs before its median is shown to anyone.
    /// 
    /// Thirty is the conventional floor at which a median stops being an anecdote, and it is
    /// deliberately below the company ladder's fifty (CompanyIntelligenceOptions.HiddenBelow):
    /// that number guards a figure printed beside a named employer, where being wrong is unfair to
    /// someone specific. This one guards an anonymous sector median with no name attached, so the
    /// bar is about statistics rather than fairness to a third party.
    ///
    /// Raising it only ever withholds more, so it is safe to raise once real distributions are
    /// visible. Lowering it is a decision about what may be published, not a tuning knob.
    /// </summary>
    public int MinimumSampleSize { get; init; } = 30;
}
