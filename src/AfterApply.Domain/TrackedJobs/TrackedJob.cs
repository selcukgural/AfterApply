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

    /// <summary>The shared Job row, when the posting came from the browser extension (which resolves
    /// one the same way "I Applied" does). Null for a manually typed entry. Carried onto the
    /// Application on conversion.</summary>
    public Guid? JobId { get; private set; }

    /// <summary>The posting's formatted description as the extension captured it — the main reason
    /// to save a posting before applying is that it disappears once the job closes. Same
    /// per-user, untrusted-HTML semantics as Application.CapturedJobDescriptionHtml, onto which it
    /// moves on conversion.</summary>
    public string? CapturedJobDescriptionHtml { get; private set; }

    private TrackedJob()
    {
    }

    public static TrackedJob Create(Guid userId, Guid companyId, string jobTitle, string? jobUrl,
        string? location, string? notes, DateTimeOffset now,
        string? hrName = null, string? hrEmail = null, string? hrLinkedInUrl = null,
        Guid? jobId = null, string? capturedJobDescriptionHtml = null)
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
            JobId = jobId,
            CapturedJobDescriptionHtml = capturedJobDescriptionHtml,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
