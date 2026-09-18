using AfterApply.Domain.Common;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.CompanySalaries;

namespace AfterApply.Application.CompanySalaries.Contracts;

// Three shapes, never mixed: what a signed-in reader sees of other people's rows (no author, the
// experience band instead of the years, the period in years), what the author sees of
// their own, and — behind the admin gate only — the row with its author joined. The public
// record must not gain the years or an author field "because it is handy" — the anonymity
// promise on the privacy page is exactly this file. Nothing here was typed by a user: the
// occupation is a catalogue row.

public sealed record CompanySalaryPublicResponse(
    Guid Id,
    OccupationRefResponse Occupation,
    ExperienceBand ExperienceBand,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    /// <summary>yyyy-MM of the submission — month precision on purpose. Since the period exists
    /// (2026-09-18) the page shows it only on a row that has no period.</summary>
    string SubmittedMonth,
    /// <summary>The years the salary was drawn in; both null on a row written before the period
    /// existed whose author has not edited it since. Year precision on purpose — a month next to
    /// an occupation at a small company would name a person.</summary>
    int? PeriodStartYear = null,
    int? PeriodEndYear = null,
    /// <summary>Whether the row counts as current — <see cref="Domain.CompanySalaries.SalaryPeriods"/>.
    /// The list puts current rows first; the page draws the rest under a "previous periods" line.</summary>
    bool IsCurrentPeriod = true);

/// <summary>Per currency, because a median across TRY and EUR rows means nothing, and over the
/// current rows only — a 2012 salary is not what the company pays. The three figures are null
/// below <see cref="CompanySalaryPageResponse.MinimumForStats"/>.</summary>
public sealed record SalaryCurrencyStatResponse(
    SalaryCurrency Currency,
    int Count,
    decimal? MedianMonthlyNet,
    decimal? MinMonthlyNet,
    decimal? MaxMonthlyNet);

public sealed record CompanySalaryPageResponse(
    IReadOnlyList<CompanySalaryPublicResponse> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<SalaryCurrencyStatResponse> Stats,
    int MinimumForStats,
    /// <summary>How many of <see cref="Total"/> are not current — the count on the "previous
    /// periods" line, which the page draws once the current rows run out.</summary>
    int PreviousPeriodTotal = 0,
    /// <summary>The window behind "current", for the page's "how it is calculated" text.</summary>
    int CurrentWindowYears = 2);

/// <summary>The author's view of their own row: everything, including the exact years.</summary>
public sealed record MyCompanySalaryResponse(
    Guid Id,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    OccupationRefResponse Occupation,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    int? PeriodStartYear = null,
    int? PeriodEndYear = null);

public sealed record SalaryQuotaResponse(int Used, int Limit);

/// <summary>The admin table's row (2026-09-18): the author's full record plus who wrote it, the
/// one surface besides the review moderation detail where an author is joined to a response.
/// Carries everything the detail modal shows, so there is no separate detail endpoint.</summary>
public sealed record AdminCompanySalaryListItemResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    Guid AuthorUserId,
    string AuthorEmail,
    OccupationRefResponse Occupation,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    int? PeriodStartYear = null,
    int? PeriodEndYear = null);

public sealed record MySalariesResponse(IReadOnlyList<MyCompanySalaryResponse> Items, SalaryQuotaResponse Quota);

/// <summary>What a signed-in reader needs on top of the company's list: their own entries for
/// this company and how much quota is left — the contribute page's "you have N here" line.</summary>
public sealed record CompanySalaryViewerStateResponse(
    IReadOnlyList<MyCompanySalaryResponse> OwnEntries,
    SalaryQuotaResponse Quota);
