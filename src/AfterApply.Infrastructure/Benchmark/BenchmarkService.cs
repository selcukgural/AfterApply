using AfterApply.Application.Benchmark;
using AfterApply.Application.Benchmark.Contracts;
using AfterApply.Domain.Benchmark;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Benchmark;

internal sealed class BenchmarkService(AppDbContext dbContext, IOptions<BenchmarkOptions> options)
    : IBenchmarkService
{
    public async Task<BenchmarkResultResponse> SubmitAsync(
        SubmitBenchmarkRequest request, CancellationToken cancellationToken)
    {
        // The validator has already run (WithValidation on the endpoint), so the required fields
        // are present; the assertions are the compiler's, not a second check.
        var sector = request.Sector!.Value;
        var applicationCount = request.ApplicationCount!.Value;
        var replyCount = request.ReplyCount!.Value;

        var submission = BenchmarkSubmission.Create(
            applicationCount, replyCount, sector, request.Period!.Value,
            request.Seniority, request.Location, request.Locale!, DateTimeOffset.UtcNow);

        dbContext.BenchmarkSubmissions.Add(submission);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Read back *after* saving so the answer just given is part of the pools it is compared
        // against — the reader is one of the n, and the copy says so.
        //
        // Every row, not just the sector's, because a sector short of the threshold falls back to
        // the overall median. Two ints per row, and the medians are computed in memory: the right
        // shape while this table holds hundreds of rows. If it ever holds hundreds of thousands,
        // this becomes two percentile_cont queries rather than a fetch.
        var rows = await dbContext.BenchmarkSubmissions
            .AsNoTracking()
            .Select(s => new { s.Sector, s.ApplicationCount, s.ReplyCount })
            .ToListAsync(cancellationToken);

        var allRates = rows
            .Select(s => BenchmarkCalculations.ReplyRate(s.ApplicationCount, s.ReplyCount))
            .ToList();

        var cellRates = rows
            .Where(s => s.Sector == sector)
            .Select(s => BenchmarkCalculations.ReplyRate(s.ApplicationCount, s.ReplyCount))
            .ToList();

        var comparison = BenchmarkCalculations.Compare(
            BenchmarkCalculations.ReplyRate(applicationCount, replyCount),
            cellRates, allRates, options.Value.MinimumSampleSize);

        return new BenchmarkResultResponse(
            sector, applicationCount, replyCount,
            comparison.YourRate, comparison.SampleSize, comparison.TotalSubmissions,
            options.Value.MinimumSampleSize, comparison.Scope, comparison.ComparedAgainstCount,
            comparison.MedianRate, comparison.ShareBelowYou);
    }

    public async Task<BenchmarkSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var bySector = await dbContext.BenchmarkSubmissions
            .AsNoTracking()
            .GroupBy(s => s.Sector)
            .Select(g => new BenchmarkSectorCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return new BenchmarkSummaryResponse(
            bySector.Sum(s => s.Count),
            options.Value.MinimumSampleSize,
            bySector.OrderByDescending(s => s.Count).ToList());
    }
}
