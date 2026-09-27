namespace AfterApply.Application.SalaryMarket.Contracts;

/// <summary>Monthly net TRY at five percentiles, plus how many answers it rests on. The raw
/// lowest and highest answers are never part of it (DECISIONS.md 2026-09-27): they are either
/// mistakes or one person.</summary>
public sealed record SalaryStatsResponse(int Count, int P10, int P25, int P50, int P75, int P90, IReadOnlyList<string> AtLeast)
{
    public static SalaryStatsResponse From(SalaryStats stats) => new(stats.Count, stats.P10, stats.P25, stats.P50, stats.P75, stats.P90,
        Enum.GetValues<SalaryPercentiles>()
            .Where(flag => flag != SalaryPercentiles.None && stats.AtLeast.HasFlag(flag))
            .Select(flag => flag.ToString())
            .ToList());
}

/// <summary>A survey behind a year's figures, named on the page with its size and month.</summary>
public sealed record SalarySurveyEditionResponse(int Year, string SourceCode, string SourceName, string SourceUrl,
    string PublishedMonth, int Responses, int Used);

/// <summary>One year of an occupation's median, for the list page's trend line.</summary>
public sealed record SalaryTrendPointResponse(int Year, int Count, int P50);

/// <summary>A row of the occupations list: the latest year's figures, the year before's median
/// for the change, and every published year's median.</summary>
public sealed record SalaryOccupationSummaryResponse(
    string Slug,
    string NameTr,
    string NameEn,
    int LatestYear,
    SalaryStatsResponse Latest,
    int? PreviousYearP50,
    IReadOnlyList<SalaryTrendPointResponse> Trend);

public sealed record SalaryOccupationsResponse(
    int MinimumResponses,
    IReadOnlyList<SalarySurveyEditionResponse> Editions,
    IReadOnlyList<SalaryOccupationSummaryResponse> Occupations);

public sealed record SalaryLevelStatsResponse(string Level, SalaryStatsResponse Stats);

public sealed record SalaryExperienceStatsResponse(string Experience, SalaryStatsResponse Stats);

/// <summary>One year of an occupation. A level or experience range below the threshold is
/// simply absent from its list.</summary>
public sealed record SalaryOccupationYearResponse(
    int Year,
    SalaryStatsResponse Overall,
    IReadOnlyList<SalaryLevelStatsResponse> Levels,
    IReadOnlyList<SalaryExperienceStatsResponse> Experience);

public sealed record SalaryOccupationResponse(
    string Slug,
    string NameTr,
    string NameEn,
    int MinimumResponses,
    IReadOnlyList<SalaryOccupationYearResponse> Years,
    IReadOnlyList<SalarySurveyEditionResponse> Editions);
