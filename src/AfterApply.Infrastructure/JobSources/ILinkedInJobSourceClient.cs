using AfterApply.Domain.Common;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The two calls a job source answers, one each, no policy: the budget, the ledger and the
/// stop-on-block rule are the sweep's. Never throws for anything the source did — a 429, a 403,
/// a login wall, a dropped connection all come back as an outcome, because the caller has to
/// write a ledger row either way. One implementation per <see cref="Source"/>; the sweep picks
/// the client by the query's source.
/// </summary>
public interface IJobSourceClient
{
    Source Source { get; }

    /// <param name="page">1-based; each client turns it into whatever its site pages by.</param>
    Task<JobSourceFetchResult<JobSourceSearchPage>> SearchAsync(JobSourceQuery query, int page, CancellationToken cancellationToken);

    Task<JobSourceFetchResult<JobSourceDetail>> GetPostingAsync(string externalId, CancellationToken cancellationToken);
}

/// <summary>LinkedIn's public job-search fragments. Named so the integration suite can address
/// this client's HttpClient by its type name.</summary>
public interface ILinkedInJobSourceClient : IJobSourceClient;

/// <summary>kariyer.net's server-rendered listing. Named for the same reason.</summary>
public interface IKariyerNetJobSourceClient : IJobSourceClient;

public sealed record JobSourceFetchResult<T>(JobSourceFetchOutcome Outcome, int? StatusCode, int DurationMs, T? Value)
{
    public bool IsOk => Outcome == JobSourceFetchOutcome.Ok && Value is not null;

    /// <summary>The outcomes that mean "the source does not want this traffic right now" — the
    /// sweep stops for the cooldown on either.</summary>
    public bool StopsSweep => Outcome is JobSourceFetchOutcome.RateLimited or JobSourceFetchOutcome.Blocked;
}
