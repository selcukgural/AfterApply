using AfterApply.Domain.Benchmark;

namespace AfterApply.Application.ResponseRates.Contracts;

/// <summary>
/// The figures a reader is shown, for a sector row or as the "sector median" beside a company's
/// own number. Never carries a contributor share: that is a guard, not a statistic.
/// </summary>
public sealed record ResponseRateFiguresResponse(
    int Applications,
    int Contributors,
    double ResponseRate,
    double GhostingRate,
    double InterviewRate,
    double OfferRate,
    double? PostInterviewSilenceRate,
    double? MedianFirstReplyDays,
    double ClosureRate,
    // Null below ResponseRateAggregator's sub-rate floor; the page prints a dash and the row stays.
    double? PromiseKeptRate = null,
    double? RejectionNoticeRate = null)
{
    public static ResponseRateFiguresResponse From(ResponseRateFigures figures) => new(
        figures.TotalApplications,
        figures.DistinctContributors,
        figures.ResponseRate,
        figures.GhostingRate,
        figures.InterviewRate,
        figures.OfferRate,
        figures.PostInterviewSilenceRate,
        figures.MedianFirstReplyDays,
        figures.ClosureRate,
        figures.PromiseKeptRate,
        figures.RejectionNoticeRate);
}

/// <summary>
/// One sector of the public table. <see cref="Figures"/> is null when the sector is below the
/// threshold — and then the row carries nothing else either, not even how far below: "18
/// applications from 2 people" names two people's job search as precisely as a company page
/// would, and the public page is the one surface where nobody has signed in.
/// </summary>
public sealed record SectorResponseRateRow(BenchmarkSector Sector, ResponseRateFiguresResponse? Figures);

/// <summary>
/// The thresholds the page prints rather than hard-codes, so the method text can never drift from
/// what the server actually enforces.
/// </summary>
public sealed record ResponseRateThresholdsResponse(
    int MinimumContributors,
    int MinimumApplications,
    int MaturityDays,
    int MaxContributorSharePercent);

public sealed record SectorResponseRatesResponse(
    BenchmarkPeriod Period,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<SectorResponseRateRow> Sectors,
    // Applications in the window whose company has no readable industry — they are in no row, and
    // the page says so instead of letting the table look like the whole dataset.
    int UnclassifiedApplications,
    ResponseRateThresholdsResponse Thresholds);
