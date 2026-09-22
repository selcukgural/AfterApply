using AfterApply.Application.AtsSources;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.JobSources;

namespace AfterApply.Infrastructure.AtsSources;

/// <summary>
/// Reads one posting back from the ATS that hosted it, through that ATS's own public,
/// unauthenticated job-board API — not by scraping the page. Shares
/// <see cref="JobSourceFetchResult{T}"/> with the job-source sweep so the two read alike: never
/// throws, and a refusal is a value rather than an exception.
/// </summary>
public interface IAtsJobClient
{
    /// <param name="source">Which ATS, as <c>JobPostingSourceResolver</c> classified the job URL.</param>
    /// <param name="jobUrl">The posting's page URL — Workday's API path is derived from it.</param>
    /// <param name="externalId">The <c>"{account}/{posting}"</c> id from <c>AtsJobIdExtractor</c>.</param>
    Task<JobSourceFetchResult<AtsJobPosting>> GetPostingAsync(Source source, string jobUrl, string externalId,
        CancellationToken cancellationToken);
}
