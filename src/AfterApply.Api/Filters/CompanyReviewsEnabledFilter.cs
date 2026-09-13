using AfterApply.Infrastructure.CompanyReviews;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Filters;

/// <summary>
/// Flag off → 404 for every review route, before any handler runs. Same status as "no such
/// company/review", so while the feature is dark its endpoints are not distinguishable from
/// routes that do not exist — the CompanyIntelligence pattern, applied at the group.
/// </summary>
public sealed class CompanyReviewsEnabledFilter(IOptions<CompanyReviewOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        options.Value.Enabled ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
