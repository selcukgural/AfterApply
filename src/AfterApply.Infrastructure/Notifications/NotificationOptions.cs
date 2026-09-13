namespace AfterApply.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public int FollowUpThresholdDays { get; init; } = 7;

    public int GhostingThresholdDays { get; init; } = 30;

    /// <summary>
    /// The horizon past which an application is no longer something to remind about. A nudge to
    /// follow up on a nine-year-old LinkedIn import is noise, not a reminder: beyond this many days
    /// without a real status change the scan creates nothing, retires whatever it created earlier,
    /// and the dashboard offers the stale batch as one question instead (see
    /// IApplicationService.GetStaleSummaryAsync). Must exceed GhostingThresholdDays.
    /// </summary>
    public int StaleThresholdDays { get; init; } = 90;

    public string ScanCronExpression { get; init; } = "0 3 * * *";
}
