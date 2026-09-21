using AfterApply.Application.ResponseRates;
using AfterApply.Application.ResponseRates.Contracts;
using AfterApply.Domain.Benchmark;
using AfterApply.Infrastructure.ResponseRates;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

public static class ResponseRateEndpoints
{
    public static IEndpointRouteBuilder MapResponseRateEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous and read-only: a sector table with no company and no person in it. The page is
        // server-rendered on its own revalidation cadence and the service caches the table, so
        // there is no per-route rate limit beyond the global one.
        app.MapGet("/api/response-rates/sectors", async (
                BenchmarkPeriod? period,
                ISectorResponseRateService service,
                IOptions<ResponseRateOptions> options,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Flag off → 404, the CompanyIntelligence pattern: the route's existence is not
                // distinguishable from the feature being on.
                if (!options.Value.Enabled)
                {
                    return Results.NotFound();
                }

                var chosen = period ?? BenchmarkPeriod.LastTwelveMonths;
                if (chosen == BenchmarkPeriod.Longer)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["period"] = ["The table is windowed; choose three, six or twelve months."]
                    });
                }

                httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromSeconds(options.Value.CacheSeconds)
                };
                // Public + CORS-served: the cached copy must vary on Origin (see /api/config).
                httpContext.Response.Headers.Vary = HeaderNames.Origin;

                return Results.Ok(await service.GetAsync(chosen, cancellationToken));
            })
            .WithTags("ResponseRates")
            .WithSummary("Response-rate figures by sector, company-less")
            .WithDescription("Reply rate, ghosting rate, median first reply and post-interview silence per sector, " +
                             "from tracked applications in the last three, six or twelve months. A sector below the " +
                             "contributor/application threshold is a row with null figures and nothing else. 404 while " +
                             "ResponseRates:Enabled is off.")
            .Produces<SectorResponseRatesResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
