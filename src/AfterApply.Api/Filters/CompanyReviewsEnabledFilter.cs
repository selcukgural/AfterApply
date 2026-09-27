using AfterApply.Application.FeatureFlags;

namespace AfterApply.Api.Filters;

/// <summary>
/// Flag off → 404 for every review route, before any handler runs. Same status as "no such
/// company/review", so while the feature is dark its endpoints are not distinguishable from
/// routes that do not exist — the CompanyIntelligence pattern, applied at the group.
/// </summary>
public sealed class CompanyReviewsEnabledFilter(IFeatureFlags featureFlags) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        featureFlags.IsEnabled(FeatureFlag.CompanyReviews) ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
