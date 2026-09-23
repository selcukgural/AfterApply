using AfterApply.Application.SilenceReports;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.SilenceReports;
using Shouldly;

namespace AfterApply.UnitTests.SilenceReports;

public sealed class SubmitSilenceReportRequestValidatorTests
{
    private readonly SubmitSilenceReportRequestValidator _validator = new();

    private static SubmitSilenceReportRequest Valid() => new(
        SilenceStage.AfterTechnicalInterview, SilenceWait.OneToTwoMonths, null, "tr", null);

    [Fact]
    public void A_report_with_stage_wait_and_locale_passes()
    {
        _validator.Validate(Valid()).IsValid.ShouldBeTrue();
        _validator.Validate(Valid() with { PromiseGiven = true, Source = BenchmarkSource.Eksi }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Stage_and_wait_are_required()
    {
        _validator.Validate(Valid() with { Stage = null }).IsValid.ShouldBeFalse();
        _validator.Validate(Valid() with { Wait = null }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Values_outside_the_closed_lists_are_refused()
    {
        _validator.Validate(Valid() with { Stage = (SilenceStage)99 }).IsValid.ShouldBeFalse();
        _validator.Validate(Valid() with { Wait = (SilenceWait)99 }).IsValid.ShouldBeFalse();
        _validator.Validate(Valid() with { Source = (BenchmarkSource)99 }).IsValid.ShouldBeFalse();
        _validator.Validate(Valid() with { Locale = "de" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_filled_honeypot_is_refused()
    {
        _validator.Validate(Valid() with { Website = "https://spam.example" }).IsValid.ShouldBeFalse();
    }
}

public sealed class SilenceReportMonthTests
{
    [Theory]
    [InlineData(SilenceWait.TwoToFourWeeks, "2026-09-01")] // 23 Sep − 14 days = 9 Sep
    [InlineData(SilenceWait.OneToTwoMonths, "2026-08-01")]
    [InlineData(SilenceWait.TwoToThreeMonths, "2026-07-01")]
    [InlineData(SilenceWait.OverThreeMonths, "2026-06-01")]
    public void The_silence_starts_at_the_bands_lower_bound_to_month_precision(SilenceWait wait, string expected)
    {
        var submitted = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);
        SilenceReport.SilentSinceMonthFor(submitted, wait).ShouldBe(DateOnly.Parse(expected));
    }

    [Fact]
    public void Two_weeks_back_from_early_in_a_month_lands_in_the_previous_month()
    {
        var submitted = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);
        SilenceReport.SilentSinceMonthFor(submitted, SilenceWait.TwoToFourWeeks).ShouldBe(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public void A_created_report_carries_the_computed_month_and_no_identity()
    {
        var report = SilenceReport.Create(Guid.NewGuid(), SilenceStage.AfterFinalInterview, SilenceWait.OverThreeMonths,
            true, "en", BenchmarkSource.Reddit, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        report.SilentSinceMonth.ShouldBe(new DateOnly(2025, 10, 1));
        typeof(SilenceReport).GetProperty("UserId").ShouldBeNull();
    }
}

public sealed class SilenceReportCalculationsTests
{
    private static (SilenceStage, DateOnly) R(SilenceStage stage, int year, int month) => (stage, new DateOnly(year, month, 1));

    [Fact]
    public void Below_the_report_floor_nothing_is_summarised()
    {
        var reports = Enumerable.Range(0, 4).Select(i => R(SilenceStage.AfterHrScreen, 2026, 1 + i * 3)).ToList();
        SilenceReportCalculations.Summarize(reports, minimumReports: 5, minimumQuarters: 2).ShouldBeNull();
    }

    [Fact]
    public void Enough_reports_from_one_quarter_are_still_withheld()
    {
        // One hiring round, or one person with a grievance: all in Q3.
        var reports = new[] { 7, 7, 8, 8, 9, 9 }.Select(m => R(SilenceStage.AfterTechnicalInterview, 2026, m)).ToList();
        SilenceReportCalculations.Summarize(reports, 5, 2).ShouldBeNull();
    }

    [Fact]
    public void Enough_reports_across_two_quarters_are_counted_by_stage_in_form_order()
    {
        var reports = new List<(SilenceStage, DateOnly)>
        {
            R(SilenceStage.AfterTechnicalInterview, 2026, 3),
            R(SilenceStage.AfterTechnicalInterview, 2026, 4),
            R(SilenceStage.AfterApplication, 2026, 4),
            R(SilenceStage.AfterFinalInterview, 2026, 5),
            R(SilenceStage.AfterTechnicalInterview, 2026, 6),
        };

        var summary = SilenceReportCalculations.Summarize(reports, 5, 2);

        summary.ShouldNotBeNull();
        summary.Count.ShouldBe(5);
        summary.ByStage.ShouldBe([
            new SilenceStageCount(SilenceStage.AfterApplication, 1),
            new SilenceStageCount(SilenceStage.AfterTechnicalInterview, 3),
            new SilenceStageCount(SilenceStage.AfterFinalInterview, 1),
        ]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(12, 4)]
    public void Quarters_follow_the_calendar(int month, int quarter)
    {
        SilenceReportCalculations.QuarterOf(new DateOnly(2026, month, 1)).ShouldBe((2026, quarter));
    }
}
