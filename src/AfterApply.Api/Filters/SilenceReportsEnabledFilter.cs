using AfterApply.Application.FeatureFlags;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 before any handler runs — the <see cref="CompanyReviewsEnabledFilter"/>
/// pattern over <c>SilenceReports:Enabled</c>.</summary>
public sealed class SilenceReportsEnabledFilter(IFeatureFlags featureFlags) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        featureFlags.IsEnabled(FeatureFlag.SilenceReports) ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
