namespace AfterApply.Domain.JobSearch;

/// <summary>
/// The four JSearch (RapidAPI) operations the product calls. Stored as text on the cache and
/// ledger rows so a reordered enum can never rewrite history.
/// </summary>
public enum JobSearchOperation
{
    Search,
    JobDetails,
    EstimatedSalary,
    CompanyJobSalary
}
