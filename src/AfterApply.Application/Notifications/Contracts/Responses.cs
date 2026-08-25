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
