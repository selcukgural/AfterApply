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
/// from <see cref="YearsOfExperience"/>, the month of <see cref="SubmittedAt"/>, and the amounts.
/// The exact years and the author stay in this row.
/// </summary>
public sealed class CompanySalaryEntry : AuditableEntity
{
    public const int MinYearsOfExperience = 0;
    public const int MaxYearsOfExperience = 50;
    public const decimal MinAmount = 1m;
    public const decimal MaxAmount = 10_000_000m;

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

    /// <summary>When the current figures were submitted — reset on every edit, so the public
    /// "September 2026" label describes what is on screen.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    private CompanySalaryEntry()
    {
    }

    public static CompanySalaryEntry Create(Guid userId, Guid companyId, SalaryContent content, DateTimeOffset now)
    {
        content.Validate();

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
        content.Validate();
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
public readonly record struct SalaryContent(
    Guid OccupationId,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount)
{
    public void Validate()
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
    }
}

public sealed class SalaryContentInvalidException()
    : DomainException("COMPANY_SALARY_CONTENT_INVALID",
        "An occupation is required, experience must be 0–50 years, and amounts between 1 and 10,000,000.");
