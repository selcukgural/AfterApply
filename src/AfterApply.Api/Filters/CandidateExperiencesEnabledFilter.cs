using AfterApply.Application.FeatureFlags;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 for every candidate-experience route, before any handler runs — the
/// <see cref="CompanyReviewsEnabledFilter"/> pattern over <c>CandidateExperiences:Enabled</c>.</summary>
public sealed class CandidateExperiencesEnabledFilter(IFeatureFlags featureFlags) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        featureFlags.IsEnabled(FeatureFlag.CandidateExperiences) ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
