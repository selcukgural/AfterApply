using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Notifications.Contracts;

public sealed record ReminderResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    ReminderType Type,
    int DaysElapsed,
    DateTimeOffset CreatedAt,
    // The median number of days this user's own applications took to get a first reply — the
    // n=1 norm a "possibly ghosted" row is measured against ("you usually hear back in 9 days").
    // Null until enough of their applications have been answered to make a median worth showing
    // (ReminderCalculations.UserMedianResponseDays), and the same value on every row of a page.
    int? UserMedianResponseDays = null);

/// <summary>How many reminders a bulk answer actually closed. Ids that were not the caller's, or
/// were already closed, simply do not count — an id in a request body is a claim, not proof.</summary>
public sealed record BulkReminderResponse(int Affected);
