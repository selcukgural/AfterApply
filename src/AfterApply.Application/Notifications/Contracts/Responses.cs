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

/// <summary>Where the user's break stands. <c>None</c>: no break. <c>Paused</c>: reminders are
/// hidden until <see cref="ReminderPauseResponse.PausedUntil"/>. <c>Returned</c>: the break has ended
/// and the return question has not been answered yet.</summary>
public enum ReminderPauseState
{
    None,
    Paused,
    Returned
}

/// <param name="SilencedCount">Applications whose "possibly ghosted" reminder was raised during the
/// break and is still open — the number the return question offers to close. Zero outside
/// <see cref="ReminderPauseState.Returned"/>.</param>
public sealed record ReminderPauseResponse(
    ReminderPauseState State,
    DateTimeOffset? PausedFrom,
    DateTimeOffset? PausedUntil,
    int SilencedCount);
