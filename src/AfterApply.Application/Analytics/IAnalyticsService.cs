using AfterApply.Application.Analytics.Contracts;

namespace AfterApply.Application.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(Guid userId, CancellationToken cancellationToken);

    Task<ApplicationFlowResponse> GetFlowAsync(Guid userId, FlowPeriod period, CancellationToken cancellationToken);
}
