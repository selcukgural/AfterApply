namespace AfterApply.Domain.Notifications;

/// <summary>
/// Why a reminder that fell due on a day nobody answers e-mail was held back to the next working
/// day (see <c>BusinessCalendar</c>). Stored by name, so a member is never renamed.
/// </summary>
public enum ReminderDeferral
{
    Weekend,
    NewYear,
    NationalSovereigntyDay,
    LabourDay,
    YouthDay,
    DemocracyDay,
    VictoryDay,
    RepublicDay,
    RamadanFeast,
    SacrificeFeast
}
