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

    /// <summary>
    /// Hold a follow-up or missed-promise reminder that falls due on a weekend or a Turkish public
    /// holiday back to the next working morning (BusinessCalendar). On by default; also the switch
    /// to turn the behaviour off without a deploy of code.
    /// </summary>
    public bool HoldOutreachOnDaysOff { get; init; } = true;

    /// <summary>How long a contribution notification the user has read or cleared — and a reader's
    /// entry in the first-mark ledger — is kept. Unread rows are never purged.</summary>
    public int ContributionRetentionDays { get; init; } = 90;

    public string ContributionPurgeCronExpression { get; init; } = "30 4 * * *";
}
