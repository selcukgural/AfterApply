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
    /// display (spec §11 follow-up). Independent of Description, which stays plain text for the
    /// AI Job Matching prompt (Sprint 8) — no formatting overhead there. Untrusted content:
    /// callers must re-sanitize before ever rendering it (see web's DOMPurify usage), the
    /// extension's own allow-list is not a substitute for that at render time.</summary>
    public string? DescriptionHtml { get; private set; }

    public string? Url { get; private set; }

    public Source Source { get; private set; }

    public string? ExternalId { get; private set; }

    public string? Location { get; private set; }

    public RemoteType? RemoteType { get; private set; }

    public EmploymentType? EmploymentType { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    private Job()
    {
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
