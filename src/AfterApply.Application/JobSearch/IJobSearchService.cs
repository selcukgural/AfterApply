using AfterApply.Application.JobSearch.Contracts;

namespace AfterApply.Application.JobSearch;

/// <summary>
/// The product's face on JSearch. Every call is scoped to the user it counts against: the ledger
/// row, the daily ceiling and the saved preferences are all theirs, while the answers themselves
/// (postings, salary estimates) are shared and come from our own tables whenever they can.
/// </summary>
public interface IJobSearchService
{
    Task<JobSearchResultsResponse> SearchAsync(Guid userId, SearchJobsQuery query, CancellationToken cancellationToken);

    Task<JobSearchJobDetailsResponse> GetJobDetailsAsync(Guid userId, GetJobDetailsQuery query, CancellationToken cancellationToken);

    Task<JobSearchSalaryEstimatesResponse> GetEstimatedSalaryAsync(Guid userId, EstimatedSalaryQuery query, CancellationToken cancellationToken);

    Task<JobSearchCompanySalariesResponse> GetCompanyJobSalaryAsync(Guid userId, CompanyJobSalaryQuery query, CancellationToken cancellationToken);

    Task<JobSearchUsageResponse> GetUsageAsync(Guid userId, CancellationToken cancellationToken);
}
