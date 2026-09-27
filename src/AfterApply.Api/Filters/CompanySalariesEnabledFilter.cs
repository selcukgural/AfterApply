using AfterApply.Application.FeatureFlags;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 for every salary route, before any handler runs — the
/// <see cref="CompanyReviewsEnabledFilter"/> pattern over <c>CompanySalaries:Enabled</c>.</summary>
public sealed class CompanySalariesEnabledFilter(IFeatureFlags featureFlags) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        featureFlags.IsEnabled(FeatureFlag.CompanySalaries) ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
