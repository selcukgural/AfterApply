using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The two LinkedIn calls, one each, no policy: the budget, the ledger and the stop-on-block
/// rule are the sweep's. Never throws for anything the source did — a 429, a 403, a login wall,
/// a dropped connection all come back as an outcome, because the caller has to write a ledger
/// row either way.
/// </summary>
public interface ILinkedInJobSourceClient
{
    Task<JobSourceFetchResult<IReadOnlyList<JobSourceCard>>> SearchAsync(JobSourceQuery query, int start, CancellationToken cancellationToken);

    Task<JobSourceFetchResult<JobSourceDetail>> GetPostingAsync(string externalId, CancellationToken cancellationToken);
}

public sealed record JobSourceFetchResult<T>(JobSourceFetchOutcome Outcome, int? StatusCode, int DurationMs, T? Value)
{
    public bool IsOk => Outcome == JobSourceFetchOutcome.Ok && Value is not null;

    /// <summary>The outcomes that mean "the source does not want this traffic right now" — the
    /// sweep stops for the cooldown on either.</summary>
    public bool StopsSweep => Outcome is JobSourceFetchOutcome.RateLimited or JobSourceFetchOutcome.Blocked;
}
