using AfterApply.Application.ResponseRates.Contracts;
using AfterApply.Domain.Benchmark;

namespace AfterApply.Application.ResponseRates;

public interface ISectorResponseRateService
{
    /// <summary>The public table for one period. Cached; every caller sees the same rows.</summary>
    Task<SectorResponseRatesResponse> GetAsync(BenchmarkPeriod period, CancellationToken cancellationToken);

    /// <summary>
    /// One sector's visible figures for the twelve-month window, or null when that sector is below
    /// the threshold — what a company page shows as "the sector median" next to its own number.
    /// </summary>
    Task<ResponseRateFiguresResponse?> GetSectorFiguresAsync(BenchmarkSector sector, CancellationToken cancellationToken);
}
