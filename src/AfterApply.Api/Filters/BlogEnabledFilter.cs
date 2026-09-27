using AfterApply.Application.FeatureFlags;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 for every blog route, the admin ones included, before any handler
/// runs — the <see cref="CandidateExperiencesEnabledFilter"/> pattern over <c>Blog:Enabled</c>.</summary>
public sealed class BlogEnabledFilter(IFeatureFlags featureFlags) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        featureFlags.IsEnabled(FeatureFlag.Blog) ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
