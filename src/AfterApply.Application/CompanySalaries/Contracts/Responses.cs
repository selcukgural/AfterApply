using AfterApply.Domain.Common;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.CompanySalaries;

namespace AfterApply.Application.CompanySalaries.Contracts;

// Two shapes, never mixed: what a signed-in reader sees of other people's rows (no author, the
// experience band instead of the years, a month instead of a date) and what the author sees of
// their own. The public record must not gain the years or an author field "because it is handy"
// — the anonymity promise on the privacy page is exactly this file. Nothing here was typed by a
// user: the occupation is a catalogue row.

public sealed record CompanySalaryPublicResponse(
    Guid Id,
    OccupationRefResponse Occupation,
    ExperienceBand ExperienceBand,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    /// <summary>yyyy-MM of the submission — month precision on purpose.</summary>
    string SubmittedMonth);

/// <summary>Per currency, because a median across TRY and EUR rows means nothing. The three
/// figures are null below <see cref="CompanySalaryPageResponse.MinimumForStats"/>.</summary>
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
    int MinimumForStats);

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
    DateTimeOffset UpdatedAt);

public sealed record SalaryQuotaResponse(int Used, int Limit);

public sealed record MySalariesResponse(IReadOnlyList<MyCompanySalaryResponse> Items, SalaryQuotaResponse Quota);

/// <summary>What a signed-in reader needs on top of the company's list: their own entries for
/// this company and how much quota is left — the contribute page's "you have N here" line.</summary>
public sealed record CompanySalaryViewerStateResponse(
    IReadOnlyList<MyCompanySalaryResponse> OwnEntries,
    SalaryQuotaResponse Quota);
