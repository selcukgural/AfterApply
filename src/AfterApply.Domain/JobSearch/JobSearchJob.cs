using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSearch;

/// <summary>
/// One job posting as JSearch returned it, keyed by the provider's own <c>job_id</c> and the
/// country it was fetched for. Shared across every user: a posting is public data, and the whole
/// point of the row is that the second person to open the same posting reads it from here instead
/// of spending another request credit upstream.
///
/// <b>There is no UserId</b>, deliberately, like <c>CvScanResult</c>: nothing on this row says who
/// looked at it. Who searched for what lives on <see cref="JobSearchUsage"/> as counts only.
///
/// <see cref="Summary"/> is always present (the search endpoint fills it); <see cref="Detail"/>
/// is filled by the details endpoint and expires on its own clock, so a posting seen in a search
/// months ago still needs one fresh details credit before its full record is served.
/// Both columns hold the product's own DTO as JSON (see <see cref="SchemaVersion"/>), not the
/// provider's wire shape — a row is read straight back into the type the API returns.
/// </summary>
public sealed class JobSearchJob : Entity
{
    /// <summary>Provider job ids are ~400-character composite tokens (measured 402 on
    /// 2026-09-12); the documentation's 24-character samples are not what v5 returns.</summary>
    public const int MaxJobIdLength = 1000;

    public string JobId { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, lower-case. Part of the key: JSearch answers the same id
    /// differently per country (apply links, localisation), so they are separate rows.</summary>
    public string Country { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string? EmployerName { get; private set; }

    public string? Publisher { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? Detail { get; private set; }

    /// <summary>Version of the DTO JSON in <see cref="Summary"/>/<see cref="Detail"/>. A row
    /// written by an older build is treated as a miss rather than deserialised into a type it
    /// no longer matches.</summary>
    public int SchemaVersion { get; private set; }

    public DateTimeOffset SummaryFetchedAt { get; private set; }

    public DateTimeOffset? DetailFetchedAt { get; private set; }

    public DateTimeOffset? DetailExpiresAt { get; private set; }

    private JobSearchJob()
    {
    }

    public static JobSearchJob CreateFromSummary(string jobId, string country, string? title, string? employerName,
        string? publisher, string summaryJson, int schemaVersion, DateTimeOffset now)
    {
        var job = new JobSearchJob { JobId = jobId, Country = country };
        job.UpdateSummary(title, employerName, publisher, summaryJson, schemaVersion, now);
        return job;
    }

    public void UpdateSummary(string? title, string? employerName, string? publisher, string summaryJson,
        int schemaVersion, DateTimeOffset now)
    {
        Title = title;
        EmployerName = employerName;
        Publisher = publisher;
        Summary = summaryJson;
        SummaryFetchedAt = now;

        // A schema bump invalidates the detail too: it was written by the same older build.
        if (SchemaVersion != schemaVersion)
        {
            Detail = null;
            DetailFetchedAt = null;
            DetailExpiresAt = null;
        }

        SchemaVersion = schemaVersion;
    }

    public void SetDetail(string detailJson, int schemaVersion, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (SchemaVersion != schemaVersion)
        {
            SchemaVersion = schemaVersion;
        }

        Detail = detailJson;
        DetailFetchedAt = now;
        DetailExpiresAt = expiresAt;
    }

    public bool HasFreshDetail(DateTimeOffset now, int schemaVersion) =>
        Detail is not null && SchemaVersion == schemaVersion && DetailExpiresAt is { } expires && expires > now;
}
