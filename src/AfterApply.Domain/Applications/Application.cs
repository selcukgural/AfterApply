using AfterApply.Domain.Common;

namespace AfterApply.Domain.Applications;

public sealed class Application : AuditableEntity
{
    private readonly List<ApplicationEvent> _events = [];
    private readonly List<ApplicationStatusHistory> _statusHistory = [];

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid? JobId { get; private set; }

    public string JobTitle { get; private set; } = string.Empty;

    public string? JobUrl { get; private set; }

    public string? Location { get; private set; }

    public EmploymentType EmploymentType { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public Source Source { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<ApplicationEvent> Events => _events;

    public IReadOnlyCollection<ApplicationStatusHistory> StatusHistory => _statusHistory;

    private Application()
    {
    }

    public static Application Create(Guid userId, Guid companyId, string jobTitle, string? jobUrl,
        string? location, EmploymentType employmentType, DateTimeOffset appliedAt, Source source,
        string? notes, DateTimeOffset now)
    {
        var application = new Application
        {
            UserId = userId,
            CompanyId = companyId,
            JobTitle = jobTitle,
            JobUrl = jobUrl,
            Location = location,
            EmploymentType = employmentType,
            AppliedAt = appliedAt,
            Source = source,
            Notes = notes,
            Status = ApplicationStatus.Applied,
            CreatedAt = now,
            UpdatedAt = now
        };

        application._statusHistory.Add(ApplicationStatusHistory.Create(
            application.Id, fromStatus: null, ApplicationStatus.Applied, now, note: null));
        application._events.Add(ApplicationEvent.Create(
            application.Id, ApplicationEventType.ApplicationCreated, now, source, metadata: null));

        return application;
    }

    public void UpdateDetails(string jobTitle, string? jobUrl, string? location,
        EmploymentType employmentType, DateTimeOffset appliedAt, string? notes, DateTimeOffset now)
    {
        JobTitle = jobTitle;
        JobUrl = jobUrl;
        Location = location;
        EmploymentType = employmentType;
        AppliedAt = appliedAt;
        Notes = notes;
        Touch(now);
    }

    public void ChangeStatus(ApplicationStatus newStatus, DateTimeOffset changedAt, Source source, string? note)
    {
        if (newStatus == Status)
        {
            throw new InvalidOperationException("Application is already in this status.");
        }

        var fromStatus = Status;
        Status = newStatus;
        Touch(changedAt);

        _statusHistory.Add(ApplicationStatusHistory.Create(Id, fromStatus, newStatus, changedAt, note));
        _events.Add(ApplicationEvent.Create(Id, ApplicationEventType.StatusChanged, changedAt, source,
            metadata: $$"""{"fromStatus":"{{fromStatus}}","toStatus":"{{newStatus}}"}"""));
    }

    public void AddEvent(ApplicationEventType type, DateTimeOffset occurredAt, Source source, string? metadata)
    {
        if (type is ApplicationEventType.StatusChanged)
        {
            throw new InvalidOperationException("StatusChanged events can only be created via ChangeStatus.");
        }

        _events.Add(ApplicationEvent.Create(Id, type, occurredAt, source, metadata));
    }
}
