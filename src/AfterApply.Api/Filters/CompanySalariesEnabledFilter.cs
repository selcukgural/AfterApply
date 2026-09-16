using AfterApply.Infrastructure.CompanySalaries;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 for every salary route, before any handler runs — the
/// <see cref="CompanyReviewsEnabledFilter"/> pattern over <c>CompanySalaries:Enabled</c>.</summary>
public sealed class CompanySalariesEnabledFilter(IOptions<CompanySalaryOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        options.Value.Enabled ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
