namespace AfterApply.Infrastructure.Metrics;

/// <summary>
/// The product-metrics snapshot job, bound from the <c>Metrics</c> section.
///
/// Config-driven rather than a hard-coded <c>Cron.Daily()</c>, the same way
/// NotificationOptions.ScanCronExpression already is — the right cadence depends on how much data
/// there is to chew through, and that is an operational judgement, not a code one.
/// </summary>
public sealed class ProductMetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Every 30 minutes by default, not once a day.
    ///
    /// The row is still keyed on the UTC day and still overwritten in place, so the cadence does
    /// not change what is stored — only how fresh today's row is. Daily meant the page showed
    /// numbers from last midnight and, worse, showed nothing at all for up to 24 hours after a
    /// fresh deploy. Every measure in the snapshot is an "as of now" reading (weekly actives,
    /// D7/D30/D90 retention, last-30-days counts are all rolling windows), so recomputing mid-day
    /// is meaningful rather than merely repeated work.
    ///
    /// The cost to watch: ComputeSnapshotAsync pulls every user, application and status-history row
    /// into memory and aggregates in LINQ-to-objects. That is comfortable at this product's size
    /// and would not be at a much larger one — the fix then is to push the aggregation into SQL,
    /// not to slow the job back down.
    /// </summary>
    public string SnapshotCronExpression { get; init; } = "*/30 * * * *";
}
