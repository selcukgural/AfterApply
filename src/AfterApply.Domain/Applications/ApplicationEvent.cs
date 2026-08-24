using AfterApply.Domain.Common;

namespace AfterApply.Domain.Applications;

public sealed class ApplicationEvent : Entity
{
    public Guid ApplicationId { get; private set; }

    public ApplicationEventType Type { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Source Source { get; private set; }

    public string? Metadata { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private ApplicationEvent()
    {
    }

    internal static ApplicationEvent Create(Guid applicationId, ApplicationEventType type,
        DateTimeOffset occurredAt, Source source, string? metadata)
    {
        return new ApplicationEvent
        {
            ApplicationId = applicationId,
            Type = type,
            OccurredAt = occurredAt,
            Source = source,
            Metadata = metadata,
            CreatedAt = occurredAt
        };
    }
}
