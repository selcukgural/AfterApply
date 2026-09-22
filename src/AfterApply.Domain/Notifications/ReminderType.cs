namespace AfterApply.Domain.Notifications;

public enum ReminderType
{
    FollowUp,
    PossiblyGhosted,

    /// <summary>The company gave a date to answer by, the date has passed and the application has
    /// not moved. Takes the follow-up's place — the promise is a better reason to write than a
    /// count of days — and yields to PossiblyGhosted like the follow-up does. ReferenceAt is the
    /// promised date (UTC midnight), so moving the date raises a fresh reminder.</summary>
    PromiseMissed
}
