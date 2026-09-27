using AfterApply.Application.FeatureFlags;
using AfterApply.Application.SalaryMarket;
using AfterApply.Application.SalaryMarket.Contracts;
using AfterApply.Infrastructure.SalaryMarket;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

public static class SalaryMarketEndpoints
{
    public static IEndpointRouteBuilder MapSalaryMarketEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous and read-only: survey aggregates compiled into the build, answered from memory.
        // No per-route limit beyond the global one — there is no query behind them to protect.
        var group = app.MapGroup("/api/salary-market").WithTags("SalaryMarket");

        group.MapGet("/occupations", (ISalaryMarketService service, IFeatureFlags featureFlags,
                IOptions<SalaryMarketOptions> options, HttpContext httpContext) =>
            {
                if (!featureFlags.IsEnabled(FeatureFlag.SalaryMarket))
                {
                    return Results.NotFound();
                }

                SetCacheHeaders(httpContext, options.Value);
                return Results.Ok(service.GetOccupations());
            })
            .WithSummary("Occupations with published survey salary figures")
            .WithDescription("Every occupation with at least one year at or above SalaryMarket:MinimumResponses: its latest " +
                             "year's monthly net percentiles (TRY), the year before's median and one median per year. Lists " +
                             "the surveys behind the figures. 404 while SalaryMarket:Enabled is off.")
            .Produces<SalaryOccupationsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/occupations/{slug}", (string slug, ISalaryMarketService service, IFeatureFlags featureFlags,
                IOptions<SalaryMarketOptions> options, HttpContext httpContext) =>
            {
                if (!featureFlags.IsEnabled(FeatureFlag.SalaryMarket))
                {
                    return Results.NotFound();
                }

                var occupation = service.GetOccupation(slug);
                if (occupation is null)
                {
                    return Results.NotFound();
                }

                SetCacheHeaders(httpContext, options.Value);
                return Results.Ok(occupation);
            })
            .WithSummary("One occupation's survey salary figures, year by year")
            .WithDescription("Monthly net percentiles (TRY) per survey year, split by level and by experience range; a " +
                             "slice under the threshold is left out. 404 for an unknown slug, one with no published year, " +
                             "or while SalaryMarket:Enabled is off.")
            .Produces<SalaryOccupationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static void SetCacheHeaders(HttpContext httpContext, SalaryMarketOptions options)
    {
        httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(options.CacheSeconds)
        };
        // Public + CORS-served: the cached copy must vary on Origin (see /api/config).
        httpContext.Response.Headers.Vary = HeaderNames.Origin;
    }
}
