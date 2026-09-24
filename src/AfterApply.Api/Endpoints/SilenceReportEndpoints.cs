using AfterApply.Api.RateLimits;
using AfterApply.Api.Extensions;
using AfterApply.Api.Filters;
using AfterApply.Application.SilenceReports;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class SilenceReportEndpoints
{
    public static IEndpointRouteBuilder MapSilenceReportEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, like the benchmark: the report is meant to be given by someone who has never
        // heard of the product, straight from the company page they landed on. Requiring an account
        // would also attach a person to a complaint about a named employer.
        var group = app.MapGroup("/api/companies/public").WithTags("SilenceReports")
            .AddEndpointFilter<SilenceReportsEnabledFilter>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{slug}/silence-reports", async (string slug, SubmitSilenceReportRequest request,
                HttpContext httpContext, ISilenceReportService service, CancellationToken cancellationToken) =>
            {
                // The connection address only derives the repeat-block key in Redis; the report row
                // never holds it. RequestAuditMiddleware records it separately, as for every write.
                // An IPv6 caller is its /64, or a fresh address per report would pass the block.
                var requesterKey = ClientPartition.ForAddress(httpContext.Connection.RemoteIpAddress);
                var stored = await service.SubmitAsync(slug, request, requesterKey, cancellationToken);
                return stored ? Results.NoContent() : Results.NotFound();
            })
            .WithValidation<SubmitSilenceReportRequest>()
            .RequireRateLimiting(DependencyInjection.SilenceReportRateLimitPolicy)
            .WithSummary("Report that a company went silent after a hiring step")
            .WithDescription("Stores one anonymous report — the stage, a wait band and whether a reply date " +
                             "had been promised; no identifier, no free text. Nothing is shown per report: " +
                             "the company's count appears only above SilenceReports:MinimumReports across " +
                             "MinimumQuarters quarters, and only where company figures are enabled. One report " +
                             "per company per connection per SilenceReports:RepeatDays (400, coded). As with " +
                             "every write under /api, a separate request-audit row (caller IP, path, time) is " +
                             "kept and never linked to the report.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
