using AfterApply.Infrastructure.CandidateExperiences;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 for every candidate-experience route, before any handler runs — the
/// <see cref="CompanyReviewsEnabledFilter"/> pattern over <c>CandidateExperiences:Enabled</c>.</summary>
public sealed class CandidateExperiencesEnabledFilter(IOptions<CandidateExperienceOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        options.Value.Enabled ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
