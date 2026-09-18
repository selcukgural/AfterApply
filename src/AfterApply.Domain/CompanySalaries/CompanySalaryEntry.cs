using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanySalaries;

/// <summary>
/// One salary a user reports for one occupation at one company — anonymous to readers, attributed
/// to an account in the database. <c>(UserId, CompanyId, OccupationId)</c> is unique: a
/// promotion at the same company is a second row, the same occupation twice is an edit.
///
/// The occupation is a row of the seeded catalogue (<see cref="Occupations.Occupation"/>), never
/// typed text — so nothing a reader sees on this feature was written by another user. Readers
/// only ever see the projection: the occupation's name, the <see cref="ExperienceBand"/> derived
/// from <see cref="YearsOfExperience"/>, the period the salary was drawn in (years, never months),
/// and the amounts. The exact years of experience and the author stay in this row.
///
/// The period (2026-09-18) is what keeps a 2012 salary from reading as a 2026 one: readers see
/// "2010 – 2012" next to it and the company's median counts only the entries still current
/// (<see cref="SalaryPeriods"/>). Rows written before the period existed have a null
/// <see cref="PeriodStartYear"/> unless they could be backfilled — see the migration.
/// </summary>
public sealed class CompanySalaryEntry : AuditableEntity
{
    public const int MinYearsOfExperience = 0;
    public const int MaxYearsOfExperience = 50;
    public const decimal MinAmount = 1m;
    public const decimal MaxAmount = 10_000_000m;
    /// <summary>Nothing older is a salary a reader can place; the form offers no earlier year.</summary>
    public const int MinPeriodYear = 1990;

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid OccupationId { get; private set; }

    /// <summary>Total years in the profession, not at this company. Never on the public wire.</summary>
    public int YearsOfExperience { get; private set; }

    public EmploymentType EmploymentType { get; private set; }

    public SalaryEmploymentStatus EmploymentStatus { get; private set; }

    /// <summary>What lands in the account each month, before bonuses.</summary>
    public decimal MonthlyNetAmount { get; private set; }

    public SalaryCurrency Currency { get; private set; }

    /// <summary>Annual total in <see cref="Currency"/>; null means the author said there is none.
    /// The form makes "none" an explicit answer, so a null here is a statement, not a skip.</summary>
    public decimal? AnnualBonusAmount { get; private set; }

    /// <summary>
    /// The first year this salary was drawn. Null only on rows written before the period existed
    /// (2026-09-18) that could not be backfilled — a former employee's row, whose period nobody
    /// but the author knows. Every new or edited row has one.
    /// </summary>
    public int? PeriodStartYear { get; private set; }

    /// <summary>The last year this salary was drawn; null means "still drawing it" on a current
    /// employee's row, and "unknown" on a row whose <see cref="PeriodStartYear"/> is null too.</summary>
    public int? PeriodEndYear { get; private set; }

    /// <summary>When the current figures were submitted — reset on every edit. Readers see it only
    /// on a row without a period, as "shared in September 2026".</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    private CompanySalaryEntry()
    {
    }

    public static CompanySalaryEntry Create(Guid userId, Guid companyId, SalaryContent content, DateTimeOffset now)
    {
        content.Validate(now.Year);

        var entry = new CompanySalaryEntry
        {
            UserId = userId,
            CompanyId = companyId,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        entry.Apply(content);
        return entry;
    }

    public void Edit(SalaryContent content, DateTimeOffset now)
    {
        content.Validate(now.Year);
        Apply(content);
        SubmittedAt = now;
        Touch(now);
    }

    private void Apply(SalaryContent content)
    {
        OccupationId = content.OccupationId;
        YearsOfExperience = content.YearsOfExperience;
        EmploymentType = content.EmploymentType;
        EmploymentStatus = content.EmploymentStatus;
        PeriodStartYear = content.PeriodStartYear;
        PeriodEndYear = content.PeriodEndYear;
        MonthlyNetAmount = decimal.Round(content.MonthlyNetAmount, 2, MidpointRounding.AwayFromZero);
        Currency = content.Currency;
        AnnualBonusAmount = content.AnnualBonusAmount is { } bonus
            ? decimal.Round(bonus, 2, MidpointRounding.AwayFromZero)
            : null;
    }
}

/// <summary>
/// Everything a salary entry says, in one value so Create and Edit share one invariant. The
/// request validator says the same things earlier and in the user's language; this is the
/// boundary that stores the row, so it checks again.
/// </summary>
/// <param name="PeriodStartYear">Required on every write — only rows older than the field may lack
/// one, and they get it the first time their author edits them.</param>
/// <param name="PeriodEndYear">Null for a current employee (still drawing it), required for a
/// former one.</param>
public readonly record struct SalaryContent(
    Guid OccupationId,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    int PeriodStartYear,
    int? PeriodEndYear)
{
    /// <param name="currentYear">The year of the write; a period cannot reach past it.</param>
    public void Validate(int currentYear)
    {
        if (OccupationId == Guid.Empty)
        {
            throw new SalaryContentInvalidException();
        }

        if (YearsOfExperience is < CompanySalaryEntry.MinYearsOfExperience or > CompanySalaryEntry.MaxYearsOfExperience)
        {
            throw new SalaryContentInvalidException();
        }

        if (!Enum.IsDefined(EmploymentType) || !Enum.IsDefined(EmploymentStatus) || !Enum.IsDefined(Currency))
        {
            throw new SalaryContentInvalidException();
        }

        if (MonthlyNetAmount is < CompanySalaryEntry.MinAmount or > CompanySalaryEntry.MaxAmount)
        {
            throw new SalaryContentInvalidException();
        }

        if (AnnualBonusAmount is { } bonus && bonus is < CompanySalaryEntry.MinAmount or > CompanySalaryEntry.MaxAmount)
        {
            throw new SalaryContentInvalidException();
        }

        if (PeriodStartYear < CompanySalaryEntry.MinPeriodYear || PeriodStartYear > currentYear)
        {
            throw new SalaryContentInvalidException();
        }

        // "Still drawing it" is what a current employee's row says, so an end year contradicts
        // it; a former employee's row without one would sit in the current list forever.
        if (EmploymentStatus == SalaryEmploymentStatus.CurrentEmployee)
        {
            if (PeriodEndYear is not null)
            {
                throw new SalaryContentInvalidException();
            }
        }
        else if (PeriodEndYear is not { } end || end < PeriodStartYear || end > currentYear)
        {
            throw new SalaryContentInvalidException();
        }
    }
}

public sealed class SalaryContentInvalidException()
    : DomainException("COMPANY_SALARY_CONTENT_INVALID",
        "An occupation is required, experience must be 0–50 years, amounts between 1 and 10,000,000, and the period must be within 1990 and the current year.");
