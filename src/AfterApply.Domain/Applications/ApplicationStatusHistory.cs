using AfterApply.Domain.Common;

namespace AfterApply.Domain.Applications;

public sealed class ApplicationStatusHistory : Entity
{
    public Guid ApplicationId { get; private set; }

    public ApplicationStatus? FromStatus { get; private set; }

    public ApplicationStatus ToStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public string? Note { get; private set; }

    private ApplicationStatusHistory()
    {
    }

    internal static ApplicationStatusHistory Create(Guid applicationId, ApplicationStatus? fromStatus,
        ApplicationStatus toStatus, DateTimeOffset changedAt, string? note)
    {
        return new ApplicationStatusHistory
        {
            ApplicationId = applicationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedAt = changedAt,
            Note = note
        };
    }
}
