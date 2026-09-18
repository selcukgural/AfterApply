using AfterApply.Domain.Common;
using AfterApply.Domain.CompanySalaries;

namespace AfterApply.Application.CompanySalaries.Contracts;

/// <summary>One shape for create and update; the route carries the company or the entry id.
/// <see cref="HasBonus"/> makes "no bonus" an explicit answer rather than a field left blank —
/// the row only keeps the nullable amount.</summary>
public sealed record CompanySalaryRequest(
    /// <summary>A row of the occupation catalogue; typed text is never accepted.</summary>
    Guid OccupationId,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    bool HasBonus,
    decimal? AnnualBonusAmount = null,
    /// <summary>The first year the salary was drawn. Optional in the shape only so older
    /// clients still parse; the validator requires it on every write.</summary>
    int? PeriodStartYear = null,
    /// <summary>The last year, required for a former employee and forbidden for a current one —
    /// "still drawing it" is the null.</summary>
    int? PeriodEndYear = null);

public sealed record CompanySalaryListQuery(int Page = 1);

/// <summary>The admin table: an optional company-name filter, newest first.</summary>
public sealed record AdminCompanySalaryListQuery(string? Company = null, int Page = 1);
