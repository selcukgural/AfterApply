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

    /// <summary>The recruiter / hiring contact for this application, as the user recorded it.
    /// Deliberately per-user on the Application rather than on the shared Job row: Job is deduped
    /// across all users by (Source, ExternalId), so one user's correction would otherwise rewrite
    /// everybody's. It is also a third party's personal data, which has to disappear together with
    /// the row that holds it — see PRIVACY_CHECKLIST.md.</summary>
    public string? HrName { get; private set; }

    public string? HrEmail { get; private set; }

    public string? HrLinkedInUrl { get; private set; }

    /// <summary>Null exactly when HrEmail is null. See HrEmailSource for why the distinction is
    /// surfaced rather than kept internal.</summary>
    public HrEmailSource? HrEmailSource { get; private set; }

    /// <summary>Which of the user's stored CVs was sent with this application, when they said.
    /// Optional and nullable for good: the field did not exist for anything applied to before it
    /// shipped, and deleting a CV clears the reference (ON DELETE SET NULL) rather than taking the
    /// application with it — a deleted file must not erase a piece of the user's own history.
    /// Ownership is checked at the service boundary, since a CV id is a caller-supplied id like
    /// any other.</summary>
    public Guid? CvDocumentId { get; private set; }

    public IReadOnlyCollection<ApplicationEvent> Events => _events;

    public IReadOnlyCollection<ApplicationStatusHistory> StatusHistory => _statusHistory;

    private Application()
    {
    }

    public static Application Create(Guid userId, Guid companyId, string jobTitle, string? jobUrl,
        string? location, EmploymentType employmentType, DateTimeOffset appliedAt, Source source,
        string? notes, DateTimeOffset now, Guid? jobId = null,
        string? hrName = null, string? hrEmail = null, string? hrLinkedInUrl = null,
        Guid? cvDocumentId = null)
    {
        var application = new Application
        {
            UserId = userId,
            CompanyId = companyId,
            JobId = jobId,
            JobTitle = jobTitle,
            JobUrl = jobUrl,
            Location = location,
            EmploymentType = employmentType,
            AppliedAt = appliedAt,
            Source = source,
            Notes = notes,
            HrName = hrName,
            HrEmail = hrEmail,
            HrEmailSource = hrEmail is null ? null : Applications.HrEmailSource.Manual,
            HrLinkedInUrl = hrLinkedInUrl,
            CvDocumentId = cvDocumentId,
            Status = ApplicationStatus.Applied,
            CreatedAt = now,
            UpdatedAt = now
        };

        // The seed row is stamped with appliedAt, not now: it records when the application entered
        // the Applied status, which is the day the user applied, not the day the row was written.
        // These differ for anything imported (the CSV carries a past date) and for a manual entry
        // backdated by the user — and the status history is ordered by ChangedAt, so stamping it
        // "now" would sort the very first row after later transitions.
        application._statusHistory.Add(ApplicationStatusHistory.Create(
            application.Id, fromStatus: null, ApplicationStatus.Applied, appliedAt,
            new StatusChangeContext(source, OriginFor(source))));
        application._events.Add(ApplicationEvent.Create(
            application.Id, ApplicationEventType.ApplicationCreated, now, source, metadata: null));

        return application;
    }

    public void UpdateDetails(string jobTitle, string? jobUrl, string? location,
        EmploymentType employmentType, DateTimeOffset appliedAt, string? notes, DateTimeOffset now,
        string? hrName = null, string? hrEmail = null, string? hrLinkedInUrl = null,
        Guid? cvDocumentId = null)
    {
        JobTitle = jobTitle;
        JobUrl = jobUrl;
        Location = location;
        EmploymentType = employmentType;
        AppliedAt = appliedAt;
        Notes = notes;
        // Straight assignment, not fill-if-missing: this is the edit form, so clearing a field the
        // user emptied is the whole point. Automatic sources (a later phase fills HrEmail from a
        // matched email) must go through their own fill-if-missing path instead of this one.
        HrName = hrName;
        HrEmail = hrEmail;
        // Anything arriving through the edit form is the user's own assertion, including a value
        // they left alone after it was auto-filled — once they have saved the form with it, it is
        // theirs. Clearing the address clears the provenance with it.
        HrEmailSource = hrEmail is null ? null : Applications.HrEmailSource.Manual;
        HrLinkedInUrl = hrLinkedInUrl;
        // Same straight assignment as the fields above: the edit form is where a user detaches a
        // CV from an application, so null has to mean "clear it".
        CvDocumentId = cvDocumentId;
        Touch(now);
    }

    /// <summary>
    /// Fills the HR email from the sender of an email that was matched to this application, and
    /// only when there is nothing there yet — the user's own entry always wins over a guess, no
    /// matter how confident. Deliberately separate from UpdateDetails, which assigns straight
    /// through because clearing a field is the point of an edit form.
    /// </summary>
    public void SetHrEmailFromIncomingEmail(string email, DateTimeOffset now)
    {
        if (HrEmail is not null)
        {
            return;
        }

        HrEmail = email;
        HrEmailSource = Applications.HrEmailSource.IncomingEmail;
        Touch(now);
    }

    public void ChangeStatus(ApplicationStatus newStatus, DateTimeOffset changedAt, StatusChangeContext context)
    {
        if (newStatus == Status)
        {
            throw new ApplicationAlreadyInStatusException();
        }

        var fromStatus = Status;
        Status = newStatus;
        Touch(changedAt);

        _statusHistory.Add(ApplicationStatusHistory.Create(Id, fromStatus, newStatus, changedAt, context));
        _events.Add(ApplicationEvent.Create(Id, ApplicationEventType.StatusChanged, changedAt, context.Source,
            metadata: $$"""{"fromStatus":"{{fromStatus}}","toStatus":"{{newStatus}}"}"""));
    }

    /// <summary>The origin implied by the Source a brand-new application was created with. Only used
    /// for the seed "→ Applied" history row: Create() has no separate origin argument because the
    /// caller's Source already says everything there is to say about how the row appeared.</summary>
    private static StatusChangeOrigin OriginFor(Source source) => source switch
    {
        Source.CsvImport or Source.LinkedInImport => StatusChangeOrigin.Import,
        Source.BrowserExtension => StatusChangeOrigin.Extension,
        Source.Email => StatusChangeOrigin.EmailSuggestionConfirmed,
        Source.System => StatusChangeOrigin.System,
        _ => StatusChangeOrigin.Manual
    };

    public void AddEvent(ApplicationEventType type, DateTimeOffset occurredAt, Source source, string? metadata)
    {
        if (type is ApplicationEventType.StatusChanged)
        {
            throw new StatusChangedEventNotAllowedException();
        }

        _events.Add(ApplicationEvent.Create(Id, type, occurredAt, source, metadata));
    }
}

public sealed class ApplicationAlreadyInStatusException()
    : DomainException("APPLICATION_ALREADY_IN_STATUS", "Application is already in this status.");

public sealed class StatusChangedEventNotAllowedException()
    : DomainException("STATUS_CHANGED_EVENT_INVALID", "StatusChanged events can only be created via ChangeStatus.");
