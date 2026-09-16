namespace AfterApply.Domain.CompanySalaries;

/// <summary>ISO 4217 codes, stored as their three letters. A closed list on purpose: every
/// currency in it has to be one the web can format and the stats can group by.</summary>
public enum SalaryCurrency
{
    TRY,
    EUR,
    USD,
    GBP
}
