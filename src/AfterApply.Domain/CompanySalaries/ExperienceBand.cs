namespace AfterApply.Domain.CompanySalaries;

/// <summary>
/// What readers see instead of the exact number of years. A job title is required on a salary
/// entry, and "senior backend, 7 years, former employee, September 2026" identifies a person at
/// a small company; the band is the compensating control (the reviews feature has no job title
/// at all for the same reason — DECISIONS.md 2026-09-13).
/// </summary>
public enum ExperienceBand
{
    ZeroToOne,
    TwoToFour,
    FiveToNine,
    TenPlus
}

public static class ExperienceBands
{
    public static ExperienceBand From(int yearsOfExperience) => yearsOfExperience switch
    {
        <= 1 => ExperienceBand.ZeroToOne,
        <= 4 => ExperienceBand.TwoToFour,
        <= 9 => ExperienceBand.FiveToNine,
        _ => ExperienceBand.TenPlus
    };
}
