namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// The four JSearch operations, one call each, no policy: no cache, no ledger, no ceilings —
/// those are JobSearchService's. Every method either returns the provider's data or throws
/// <see cref="JSearchException"/>; nothing is swallowed, because the caller has to write a ledger
/// row either way.
/// </summary>
public interface IJSearchClient
{
    Task<JSearchResult<JSearchSearchData>> SearchAsync(JSearchSearchRequest request, CancellationToken cancellationToken);

    Task<JSearchResult<IReadOnlyList<JSearchJob>>> GetJobDetailsAsync(JSearchJobDetailsRequest request, CancellationToken cancellationToken);

    Task<JSearchResult<IReadOnlyList<JSearchSalaryEstimate>>> GetEstimatedSalaryAsync(JSearchEstimatedSalaryRequest request, CancellationToken cancellationToken);

    Task<JSearchResult<IReadOnlyList<JSearchCompanySalary>>> GetCompanyJobSalaryAsync(JSearchCompanySalaryRequest request, CancellationToken cancellationToken);
}
