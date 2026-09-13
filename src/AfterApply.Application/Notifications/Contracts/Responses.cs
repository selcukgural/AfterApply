using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Notifications.Contracts;

public sealed record ReminderResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    ReminderType Type,
    int DaysElapsed,
    DateTimeOffset CreatedAt);

/// <summary>How many reminders a bulk answer actually closed. Ids that were not the caller's, or
/// were already closed, simply do not count — an id in a request body is a claim, not proof.</summary>
public sealed record BulkReminderResponse(int Affected);
