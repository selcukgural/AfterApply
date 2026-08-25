namespace AfterApply.Application.Metrics;

public interface IProductMetricsService
{
    Task<ProductMetricsSnapshot> ComputeSnapshotAsync(CancellationToken cancellationToken);
}
