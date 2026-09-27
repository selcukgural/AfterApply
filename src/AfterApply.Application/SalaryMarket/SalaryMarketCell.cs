namespace AfterApply.Application.SalaryMarket;

/// <summary>What a cell is cut by: the whole occupation, or one of its levels / experience ranges.</summary>
public enum SalaryMarketDimension
{
    All,
    Level,
    Experience
}

/// <summary>The survey's own seniority answer. Every source is folded onto these three.</summary>
public enum SalaryMarketLevel
{
    Junior,
    Middle,
    Senior
}

/// <summary>
/// Years in the industry, on the one scale every source can be mapped onto (DECISIONS.md
/// 2026-09-27). The surveys' own ranges do not line up (0–1 / 1–3 / 3–5… against 0–2 / 3–5 /
/// 6–10…), so each is folded into the nearest of these four.
/// </summary>
public enum SalaryMarketExperience
{
    ZeroToTwo,
    ThreeToFive,
    SixToTen,
    TenPlus
}

/// <summary>
/// Monthly net pay in TRY at the percentiles the pages show. A value the survey only knows as a
/// floor — the answer fell in an open top range such as "15.000 TL ve üzeri" — is flagged in
/// <see cref="AtLeast"/> so a page prints "≥ 15.000".
/// </summary>
public sealed record SalaryStats(int Count, int P10, int P25, int P50, int P75, int P90, SalaryPercentiles AtLeast = SalaryPercentiles.None);

[Flags]
public enum SalaryPercentiles
{
    None = 0,
    P10 = 1,
    P25 = 2,
    P50 = 4,
    P75 = 8,
    P90 = 16
}

/// <summary>
/// One published figure: a year, an occupation, and the slice it describes. <see cref="Bucket"/>
/// is the <see cref="SalaryMarketLevel"/> or <see cref="SalaryMarketExperience"/> name, empty
/// for <see cref="SalaryMarketDimension.All"/>. Only cells at or above the threshold exist.
/// </summary>
public sealed record SalaryMarketCell(int Year, string Group, SalaryMarketDimension Dimension, string Bucket, SalaryStats Stats);

/// <summary>
/// One survey that fed a year: who ran it, the month its results were published, how many answered and how many
/// of those answers made it into a figure (paid in TRY, working in Turkey, occupation known).
/// </summary>
public sealed record SalarySurveyEdition(int Year, string SourceCode, string SourceName, string SourceUrl, string PublishedMonth, int Responses, int Used);
