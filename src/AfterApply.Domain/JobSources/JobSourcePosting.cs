using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSources;

/// <summary>
/// One job posting as a job source (today: LinkedIn's public listing) published it, keyed by the
/// source and its own id. Shared across every user: a posting is public data, and the whole point
/// of the row is that a query two users share is fetched once and both are served from here.
///
/// <b>There is no UserId</b>, deliberately: nothing on this row says who was shown it. Who received
/// what lives on <see cref="UserJobSourceDelivery"/>.
///
/// Not the <c>Jobs</c> table on purpose — a <c>Job</c> row needs a <c>Company</c>, and creating a
/// company per fetched listing would pour hundreds of never-applied-to names into the table the
/// application flow resolves against.
///
/// <see cref="Description"/> is plain text, never HTML: the source's markup is untrusted input and
/// the only consumer is a model prompt, which wants text anyway.
/// </summary>
public sealed class JobSourcePosting : Entity
{
    public const int MaxExternalIdLength = 64;
    public const int MaxDescriptionLength = 20_000;

    public Source Source { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string CompanyName { get; private set; } = string.Empty;

    public string? CompanyProfileUrl { get; private set; }

    public string? Location { get; private set; }

    public DateOnly? PostedAt { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Seniority { get; private set; }

    public string? EmploymentType { get; private set; }

    public string? JobFunction { get; private set; }

    public string? Industries { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    /// <summary>Last time a search page listed this posting. Retention keys on it: a posting no
    /// query has surfaced for a while is gone from the source too.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? DetailFetchedAt { get; private set; }

    public bool HasDetail => DetailFetchedAt is not null;

    private JobSourcePosting()
    {
    }

    public static JobSourcePosting Create(Source source, string externalId, string title, string companyName,
        string? companyProfileUrl, string? location, DateOnly? postedAt, string url, DateTimeOffset now)
    {
        return new JobSourcePosting
        {
            Source = source,
            ExternalId = externalId,
            Title = title,
            CompanyName = companyName,
            CompanyProfileUrl = companyProfileUrl,
            Location = location,
            PostedAt = postedAt,
            Url = url,
            FirstSeenAt = now,
            LastSeenAt = now
        };
    }

    /// <summary>A card seen again: refresh what the card carries, keep what the detail fetch added.</summary>
    public void SeenAgain(string title, string companyName, string? companyProfileUrl, string? location,
        DateOnly? postedAt, DateTimeOffset now)
    {
        Title = title;
        CompanyName = companyName;
        CompanyProfileUrl ??= companyProfileUrl;
        Location ??= location;
        PostedAt ??= postedAt;
        LastSeenAt = now;
    }

    public void SetDetail(string? description, string? seniority, string? employmentType, string? jobFunction,
        string? industries, DateTimeOffset now)
    {
        Description = description is { Length: > MaxDescriptionLength } ? description[..MaxDescriptionLength] : description;
        Seniority = seniority;
        EmploymentType = employmentType;
        JobFunction = jobFunction;
        Industries = industries;
        DetailFetchedAt = now;
    }
}
