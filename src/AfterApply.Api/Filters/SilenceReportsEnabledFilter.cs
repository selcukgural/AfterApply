using AfterApply.Infrastructure.SilenceReports;
using Microsoft.Extensions.Options;

namespace AfterApply.Api.Filters;

/// <summary>Flag off → 404 before any handler runs — the <see cref="CompanyReviewsEnabledFilter"/>
/// pattern over <c>SilenceReports:Enabled</c>.</summary>
public sealed class SilenceReportsEnabledFilter(IOptions<SilenceReportOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        options.Value.Enabled ? next(context) : ValueTask.FromResult<object?>(Results.NotFound());
}
