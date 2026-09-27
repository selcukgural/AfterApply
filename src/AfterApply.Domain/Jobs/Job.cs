using AfterApply.Domain.Common;

namespace AfterApply.Domain.Jobs;

public sealed class Job : AuditableEntity
{
    public Guid CompanyId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>A minimal, allow-listed HTML snapshot of Description (only p/br/strong/b/em/i/
    /// ul/ol/li/h1-h6, no attributes) captured by the browser extension for a formatted read-only
    /// display (spec §11 follow-up). Independent of Description, which stays plain text (used as
    /// the JobDescriptionCard fallback, and by the email job-extraction provider, which never
    /// produces HTML). Untrusted content: callers must re-sanitize before ever rendering it (see
    /// web's DOMPurify usage), the extension's own allow-list is not a substitute for that at
    /// render time.</summary>
    public string? DescriptionHtml { get; private set; }

    public string? Url { get; private set; }

    public Source Source { get; private set; }

    public string? ExternalId { get; private set; }

    public string? Location { get; private set; }

    public RemoteType? RemoteType { get; private set; }

    public EmploymentType? EmploymentType { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// When the posting stopped taking applications, as observed by <c>JobLivenessService</c>.
    /// Final: a closed row never reopens. A posting the site puts back up is a new posting (a new
    /// row, see <c>JobResolver</c>), so everyone who applied to this round keeps seeing it closed.
    /// Only ever set from the server's own look at the site — never from anything a user reports
    /// (ghosting is about one application, and this row is shared by everyone who saved it).
    /// </summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When the site announces an open posting will close (kariyer.net publishes it).</summary>
    public DateTimeOffset? ClosesAt { get; private set; }

    /// <summary>The last time the liveness check looked at this posting.</summary>
    public DateTimeOffset? LivenessCheckedAt { get; private set; }

    /// <summary>The first of two looks that found the posting gone without the site saying why;
    /// cleared when a later look finds it up.</summary>
    public DateTimeOffset? LivenessSuspectedAt { get; private set; }

    private Job()
    {
    }

    /// <summary>
    /// Takes one liveness observation. Returns true when it closed the posting.
    /// </summary>
    /// <param name="confirmAfter">How long a "gone" has to persist, between the first look that
    /// saw it and a later one, before it counts: a bare 404 can be a bad minute on the site's
    /// side, and a close is permanent.</param>
    public bool ApplyLiveness(PostingLiveness observation, DateTimeOffset now, TimeSpan confirmAfter)
    {
        if (ClosedAt is not null)
        {
            return false;
        }

        LivenessCheckedAt = now;
        Touch(now);

        switch (observation.Kind)
        {
            case PostingLivenessKind.Open:
                LivenessSuspectedAt = null;
                ClosesAt = observation.ClosesOn ?? ClosesAt;
                return false;

            case PostingLivenessKind.Closed:
                LivenessSuspectedAt = null;
                ClosedAt = observation.ClosedOn is { } closedOn && closedOn < now ? closedOn : now;
                return true;

            case PostingLivenessKind.Gone when LivenessSuspectedAt is null:
                LivenessSuspectedAt = now;
                return false;

            case PostingLivenessKind.Gone when now - LivenessSuspectedAt >= confirmAfter:
                // Dated from the first look that saw it gone, which is the closer estimate.
                ClosedAt = LivenessSuspectedAt;
                LivenessSuspectedAt = null;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Fills in whatever the capture left blank, from a later read of the same posting through the
    /// ATS's own API (see <c>AtsJobEnrichmentService</c>). Fill-if-missing throughout, like
    /// <c>Company.EnrichFrom</c>: the extension read the page the user was actually looking at, so
    /// a value it produced outranks one fetched afterwards — the API is a backstop for the fields
    /// the scrape could not reach, not a correction of the ones it could. The description is the
    /// exception since 2026-09-24: a capture's description stays on its own application (see
    /// <c>IJobResolver</c>), so on this shared row it only ever comes from here.
    ///
    /// Title is the one exception and is never touched: it is what the person sees on their own
    /// application row, it was editable in the popup before they submitted it, and quietly
    /// rewriting an edited title would undo their correction.
    /// </summary>
    public bool EnrichFrom(string? description, string? descriptionHtml, string? location,
        EmploymentType? employmentType, DateTimeOffset? publishedAt, DateTimeOffset now)
    {
        var changed = false;

        if (Description is null && description is not null)
        {
            Description = description;
            changed = true;
        }

        if (DescriptionHtml is null && descriptionHtml is not null)
        {
            DescriptionHtml = descriptionHtml;
            changed = true;
        }

        if (Location is null && location is not null)
        {
            Location = location;
            changed = true;
        }

        if (EmploymentType is null && employmentType is not null)
        {
            EmploymentType = employmentType;
            changed = true;
        }

        if (PublishedAt is null && publishedAt is not null)
        {
            PublishedAt = publishedAt;
            changed = true;
        }

        if (changed)
        {
            Touch(now);
        }

        return changed;
    }

    public static Job Create(Guid companyId, string title, Source source, DateTimeOffset now,
        string? description = null, string? url = null, string? externalId = null,
        string? location = null, RemoteType? remoteType = null, EmploymentType? employmentType = null,
        DateTimeOffset? publishedAt = null, string? descriptionHtml = null)
    {
        return new Job
        {
            CompanyId = companyId,
            Title = title,
            NormalizedTitle = JobTitleNormalizer.Normalize(title),
            Description = description,
            DescriptionHtml = descriptionHtml,
            Url = url,
            Source = source,
            ExternalId = externalId,
            Location = location,
            RemoteType = remoteType,
            EmploymentType = employmentType,
            PublishedAt = publishedAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
