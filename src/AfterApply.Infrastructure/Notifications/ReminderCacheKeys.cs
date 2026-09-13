namespace AfterApply.Infrastructure.Notifications;

/// <summary>
/// Where the reminders list lives in the cache. One entry per page, all carrying one tag per user,
/// so the two services that invalidate it — ReminderService on any answer, ApplicationService
/// whenever a status change retires reminders (a terminal status closes them without anyone
/// pressing "dismiss") — drop every page with one call and never have to know which pages exist.
/// </summary>
internal static class ReminderCacheKeys
{
    public static string ActiveTag(Guid userId) => $"reminders:active:{userId}";

    public static string ActivePage(Guid userId, int page, int pageSize) => $"reminders:active:{userId}:p{page}:s{pageSize}";
}
