using AfterApply.Application.ResponseRates;
using AfterApply.Application.ResponseRates.Contracts;
using AfterApply.Domain.Benchmark;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.ResponseRates;

/// <summary>
/// The public table: every application in the window, grouped by the sector its company's
/// industry maps to, reduced with the same aggregator a company page uses. No UserId filter —
/// this is the one query in the tracker that reads across every account, which is why what it
/// returns is thresholds-first: a sector is a row only once enough different people are in it.
/// </summary>
internal sealed class SectorResponseRateService(
    AppDbContext dbContext, HybridCache cache, IOptions<ResponseRateOptions> options)
    : ISectorResponseRateService
{
    // One entry per period; the three keys are enumerable so a future invalidation can name them.
    private static string CacheKey(BenchmarkPeriod period) => $"response-rates:sectors:{period}";

    public async Task<SectorResponseRatesResponse> GetAsync(BenchmarkPeriod period, CancellationToken cancellationToken)
    {
        var seconds = options.Value.CacheSeconds;
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(seconds),
            LocalCacheExpiration = TimeSpan.FromSeconds(seconds)
        };

        return await cache.GetOrCreateAsync(CacheKey(period), async ct => await ComputeAsync(period, ct),
            entryOptions, cancellationToken: cancellationToken);
    }

    public async Task<ResponseRateFiguresResponse?> GetSectorFiguresAsync(BenchmarkSector sector, CancellationToken cancellationToken)
    {
        var table = await GetAsync(BenchmarkPeriod.LastTwelveMonths, cancellationToken);
        return table.Sectors.FirstOrDefault(row => row.Sector == sector)?.Figures;
    }

    private async Task<SectorResponseRatesResponse> ComputeAsync(BenchmarkPeriod period, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var windowEnd = DateTimeOffset.UtcNow;
        var windowStart = windowEnd.AddMonths(-ResponseRatePeriods.MonthsOf(period));

        var rows = await dbContext.Applications
            .Where(a => a.AppliedAt >= windowStart && a.AppliedAt <= windowEnd)
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id,
                (a, c) => new
                {
                    a.Id, a.UserId, a.Status, a.AppliedAt,
                    a.PromisedReplyBy, a.PromisedReplySince, a.RejectionNotice, c.Industry
                })
            .ToListAsync(cancellationToken);

        var applicationIds = rows.Select(r => r.Id).ToList();
        var history = await dbContext.ApplicationStatusHistories
            .Where(h => applicationIds.Contains(h.ApplicationId))
            .Select(h => new { h.ApplicationId, h.ToStatus, h.ChangedAt })
            .ToListAsync(cancellationToken);
        var historyByApplication = history
            .GroupBy(h => h.ApplicationId)
            .ToDictionary(g => g.Key, g => g.Select(h => (h.ToStatus, h.ChangedAt)).ToList());

        var unclassified = 0;
        var samplesBySector = new Dictionary<BenchmarkSector, List<ResponseRateSample>>();
        foreach (var row in rows)
        {
            var sector = IndustrySectorClassifier.Classify(row.Industry);
            if (sector is null)
            {
                unclassified++;
                continue;
            }

            var transitions = historyByApplication.GetValueOrDefault(row.Id) ?? [];
            var sample = ResponseRateAggregator.ToSample(row.Id, row.UserId, row.Status, row.AppliedAt, transitions,
                row.PromisedReplyBy, row.PromisedReplySince, row.RejectionNotice, windowEnd);
            if (!samplesBySector.TryGetValue(sector.Value, out var list))
            {
                samplesBySector[sector.Value] = list = [];
            }

            list.Add(sample);
        }

        // Every sector is a row, in enum order, so the page can list the hidden ones by name
        // ("7 sektör daha eşiğin altında") without the response saying anything about them.
        var sectors = Enum.GetValues<BenchmarkSector>()
            .Where(sector => sector != BenchmarkSector.Other)
            .Select(sector =>
            {
                if (!samplesBySector.TryGetValue(sector, out var samples))
                {
                    return new SectorResponseRateRow(sector, null);
                }

                var figures = ResponseRateAggregator.Compute(samples, windowEnd, opts.MaturityDays);
                var visible = figures.DistinctContributors >= opts.MinimumContributors
                              && figures.TotalApplications >= opts.MinimumApplications
                              && figures.MaxContributorShare <= opts.MaxContributorShare;
                return new SectorResponseRateRow(sector, visible ? ResponseRateFiguresResponse.From(figures) : null);
            })
            .ToList();

        return new SectorResponseRatesResponse(period, windowStart, windowEnd, sectors, unclassified,
            new ResponseRateThresholdsResponse(opts.MinimumContributors, opts.MinimumApplications, opts.MaturityDays,
                (int)Math.Round(opts.MaxContributorShare * 100)));
    }
}
