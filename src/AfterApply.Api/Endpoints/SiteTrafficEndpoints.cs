using AfterApply.Api.Extensions;
using AfterApply.Application.SiteTraffic;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class SiteTrafficEndpoints
{
    public static IEndpointRouteBuilder MapSiteTrafficEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, and it must stay that way: the whole point is to count visits to public pages
        // without knowing who is visiting. Note what this means for the client — the reporter must
        // NOT send an Authorization header even when the visitor happens to be signed in, because
        // an authenticated request would let a count be tied back to an account, which is precisely
        // what the Çerez Politikası promises the site does not do. See web/src/lib/analytics.
        app.MapPost("/api/site-traffic/events", async (
                RecordSiteTrafficEventRequest request, ISiteTrafficService service,
                CancellationToken cancellationToken) =>
            {
                await service.RecordAsync(request, cancellationToken);

                // 204 whether or not a count was recorded. A report that fails the allowlist is
                // dropped silently on purpose: a distinguishable response would tell a caller which
                // paths and event names are accepted, and a browser has nothing to do with the
                // answer either way.
                return Results.NoContent();
            })
            .WithValidation<RecordSiteTrafficEventRequest>()
            .RequireRateLimiting(DependencyInjection.SiteTrafficRateLimitPolicy)
            .WithTags("SiteTraffic")
            .WithSummary("Count one visit to a public page")
            .WithDescription("First-party, cookieless page counting. Stores no visitor id, session, " +
                             "IP address or user agent — only a per-day count for (event, page, " +
                             "language, referring host). Reports that are not a known public page " +
                             "are accepted and discarded.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
