using AfterApply.Application.Benchmark.Contracts;

namespace AfterApply.Application.Benchmark;

public interface IBenchmarkService
{
    /// <summary>Stores the answer and returns what it looks like against everyone else's.</summary>
    Task<BenchmarkResultResponse> SubmitAsync(SubmitBenchmarkRequest request, CancellationToken cancellationToken);

    /// <summary>Participation so far, for the page's opening state.</summary>
    Task<BenchmarkSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
}
