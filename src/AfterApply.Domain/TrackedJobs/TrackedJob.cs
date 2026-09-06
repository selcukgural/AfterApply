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

    /// <summary>Same per-user HR contact as Application's, carried across when the tracked job is
    /// converted into a real application — see Application.HrName for why it lives here and not on
    /// the shared Job row.</summary>
    public string? HrName { get; private set; }

    public string? HrEmail { get; private set; }

    public string? HrLinkedInUrl { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    private TrackedJob()
    {
    }

    public static TrackedJob Create(Guid userId, Guid companyId, string jobTitle, string? jobUrl,
        string? location, string? notes, DateTimeOffset now,
        string? hrName = null, string? hrEmail = null, string? hrLinkedInUrl = null)
    {
        return new TrackedJob
        {
            UserId = userId,
            CompanyId = companyId,
            JobTitle = jobTitle,
            JobUrl = jobUrl,
            Location = location,
            Notes = notes,
            HrName = hrName,
            HrEmail = hrEmail,
            HrLinkedInUrl = hrLinkedInUrl,
            AddedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
