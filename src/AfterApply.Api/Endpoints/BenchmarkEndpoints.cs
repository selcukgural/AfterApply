using AfterApply.Api.Extensions;
using AfterApply.Application.Benchmark;
using AfterApply.Application.Benchmark.Contracts;
using AfterApply.Infrastructure;

namespace AfterApply.Api.Endpoints;

public static class BenchmarkEndpoints
{
    public static IEndpointRouteBuilder MapBenchmarkEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous, and that is the whole point rather than an oversight: the benchmark exists to
        // be answerable by someone who has never heard of the product, before they decide whether
        // it is worth an account. Requiring a sign-in here would restrict the sample to the users
        // the product does not yet have.
        var group = app.MapGroup("/api/benchmark").WithTags("Benchmark");

        group.MapPost("/submissions", async (
                SubmitBenchmarkRequest request, IBenchmarkService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.SubmitAsync(request, cancellationToken)))
            .WithValidation<SubmitBenchmarkRequest>()
            .RequireRateLimiting(DependencyInjection.BenchmarkRateLimitPolicy)
            .WithSummary("Answer the public reply-rate benchmark")
            .WithDescription("Stores one anonymous answer — two counts and up to four coarse " +
                             "categories, no identifier and no free text — and returns how it " +
                             "compares. The comparison is withheld until the sector has enough " +
                             "answers for a median to mean anything.")
            .Produces<BenchmarkResultResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/summary", async (IBenchmarkService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetSummaryAsync(cancellationToken)))
            .WithSummary("How many have answered so far")
            .WithDescription("Participation counts and the sample size a sector needs before its " +
                             "median is shown. Public: the number of answers is the thing worth " +
                             "sharing, and the threshold is what lets a reader judge the rest.")
            .Produces<BenchmarkSummaryResponse>();

        return app;
    }
}
