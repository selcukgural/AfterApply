using AfterApply.Application.SiteStats;

namespace AfterApply.Api.Endpoints;

public static class SiteStatsEndpoints
{
    public static IEndpointRouteBuilder MapSiteStatsEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous and read-only: three totals with nothing behind them but a count. The landing
        // page fetches this server-side on its own revalidation cadence, so the endpoint itself is
        // cached (SiteStatsService) and needs no rate limit of its own beyond the global one.
        app.MapGet("/api/site-stats", async (ISiteStatsService service, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers.CacheControl = "public, max-age=3600";
                return Results.Ok(await service.GetAsync(cancellationToken));
            })
            .WithTags("SiteStats")
            .WithSummary("The site's running totals")
            .WithDescription("Completed CV scans, benchmark answers and published company reviews. " +
                             "Each figure is null until it reaches SiteStats:MinimumCount — a small " +
                             "number shown as social proof says the opposite of what it is meant to.")
            .Produces<SiteStatsResponse>();

        return app;
    }
}
