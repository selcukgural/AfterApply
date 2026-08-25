namespace AfterApply.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public int FollowUpThresholdDays { get; init; } = 7;

    public int GhostingThresholdDays { get; init; } = 30;

    public string ScanCronExpression { get; init; } = "0 3 * * *";
}
