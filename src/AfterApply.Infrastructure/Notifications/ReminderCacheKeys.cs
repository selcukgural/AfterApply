namespace AfterApply.Infrastructure.Notifications;

/// <summary>
/// The one cache key the reminders list lives under. Shared because two services write it:
/// ReminderService on dismiss, and ApplicationService whenever a status change retires reminders
/// (a terminal status closes them without anyone pressing "dismiss").
/// </summary>
internal static class ReminderCacheKeys
{
    public static string Active(Guid userId) => $"reminders:active:{userId}";
}
