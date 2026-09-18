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
    decimal? AnnualBonusAmount = null);

public sealed record CompanySalaryListQuery(int Page = 1);

/// <summary>The admin table: an optional company-name filter, newest first.</summary>
public sealed record AdminCompanySalaryListQuery(string? Company = null, int Page = 1);
