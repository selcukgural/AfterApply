namespace AfterApply.Application.Metrics;

public interface IProductMetricsService
{
    /// <summary>Computes today's numbers and stores them as that day's snapshot, overwriting the
    /// day if it was already computed. Returns what it computed.</summary>
    Task<ProductMetricsSnapshot> ComputeSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>The most recent stored days, newest first.</summary>
    Task<IReadOnlyList<ProductMetricsDayResponse>> GetRecentAsync(int days, CancellationToken cancellationToken);
}
