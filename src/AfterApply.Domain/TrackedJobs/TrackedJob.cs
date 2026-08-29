using AfterApply.Domain.Common;

namespace AfterApply.Domain.TrackedJobs;

public sealed class TrackedJob : AuditableEntity
{
    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public string JobTitle { get; private set; } = string.Empty;

    public string? JobUrl { get; private set; }

    public string? Location { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    private TrackedJob()
    {
    }

    public static TrackedJob Create(Guid userId, Guid companyId, string jobTitle, string? jobUrl,
        string? location, string? notes, DateTimeOffset now)
    {
        return new TrackedJob
        {
            UserId = userId,
            CompanyId = companyId,
            JobTitle = jobTitle,
            JobUrl = jobUrl,
            Location = location,
            Notes = notes,
            AddedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
