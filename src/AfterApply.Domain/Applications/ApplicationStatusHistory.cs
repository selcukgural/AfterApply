using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Domain.Applications;

public sealed class ApplicationStatusHistory : Entity
{
    public Guid ApplicationId { get; private set; }

    public ApplicationStatus? FromStatus { get; private set; }

    public ApplicationStatus ToStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>The user's own note, and nothing else — see <see cref="StatusChangeContext.Note"/>.
    /// Rows created before StatusChangeOrigin existed may still hold a system-generated Turkish
    /// sentence; the backfill migration deliberately left those untouched.</summary>
    public string? Note { get; private set; }

    /// <summary>How this change was applied. Set on every row: the migration backfilled existing
    /// rows rather than leaving history permanently "unknown".</summary>
    public StatusChangeOrigin Origin { get; private set; }

    public Source Source { get; private set; }

    /// <summary>The suggestion this change came from, so the UI can link back to the email. Null for
    /// every non-email origin. Intentionally not a foreign key — history must outlive the
    /// suggestion.</summary>
    public Guid? EmailSuggestionId { get; private set; }

    public RejectionReasonCategory? RejectionReasonCategory { get; private set; }

    public string? RejectionReasonDetail { get; private set; }

    private ApplicationStatusHistory()
    {
    }

    internal static ApplicationStatusHistory Create(Guid applicationId, ApplicationStatus? fromStatus,
        ApplicationStatus toStatus, DateTimeOffset changedAt, StatusChangeContext context)
    {
        return new ApplicationStatusHistory
        {
            ApplicationId = applicationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedAt = changedAt,
            Note = context.Note,
            Origin = context.Origin,
            Source = context.Source,
            EmailSuggestionId = context.EmailSuggestionId,
            RejectionReasonCategory = context.RejectionReasonCategory,
            RejectionReasonDetail = context.RejectionReasonDetail
        };
    }
}
