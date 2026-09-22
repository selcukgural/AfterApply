namespace AfterApply.Domain.Applications;

/// <summary>Where a company's "we'll get back to you by …" stands. See <see cref="ReplyPromises"/>.</summary>
public enum ReplyPromiseOutcome
{
    /// <summary>The date has not come yet and nothing has happened.</summary>
    Pending,

    /// <summary>The date has passed and the application has not moved.</summary>
    Overdue,

    /// <summary>The application moved on or before the date (grace included).</summary>
    Kept,

    /// <summary>The application moved, but after the date and its grace.</summary>
    Late,

    /// <summary>The candidate withdrew before the company's date came — nothing to judge.</summary>
    Void
}
