using AfterApply.Domain.Common;

namespace AfterApply.Domain.Jobs;

public sealed class Job : AuditableEntity
{
    public Guid CompanyId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public string? Description { get; private set; }

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
        DateTimeOffset? publishedAt = null)
    {
        return new Job
        {
            CompanyId = companyId,
            Title = title,
            NormalizedTitle = NormalizeTitle(title),
            Description = description,
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

    private static string NormalizeTitle(string title)
    {
        return string.Join(' ', TurkishTextNormalizer.FoldCase(title.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
