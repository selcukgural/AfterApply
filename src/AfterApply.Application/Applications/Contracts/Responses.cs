using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Application.Applications.Contracts;

public sealed record ApplicationSummaryResponse(
    Guid Id,
    string CompanyName,
    string JobTitle,
    ApplicationStatus Status,
    DateTimeOffset AppliedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationDetailResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    // The company's own site and LinkedIn page, so the detail view can link straight out to them.
    // Read from the Company row at response time rather than snapshotted onto the Application:
    // CompanyEnrichmentService fills Website in the background *after* the application row already
    // exists, so anything captured at creation time would stay null forever. Both are null until
    // some path has actually resolved them (a kariyer.net-only company has neither today).
    string? CompanyWebsite,
    string? CompanyLinkedInUrl,
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    ApplicationStatus Status,
    Source Source,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // Allow-listed HTML for a formatted, read-only display of the linked Job's description —
    // untrusted content, the frontend re-sanitizes with DOMPurify before ever rendering it (see
    // JobDescriptionCard). Null when there's no linked Job, or the Job predates this field.
    string? JobDescriptionHtml = null,
    // This user's own copy of the HR contact — never shared with other users who applied to the
    // same posting, and deleted with the application.
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null,
    // Null exactly when HrEmail is null — the UI labels an auto-filled address rather than passing
    // a guess off as something the user typed.
    HrEmailSource? HrEmailSource = null,
    // The CV recorded for this application, if any. The name rides along so the detail view can
    // show it without a second request; it is null when no CV is attached, and goes back to null on
    // its own if that CV is later deleted (the FK is ON DELETE SET NULL).
    Guid? CvDocumentId = null,
    string? CvDocumentFileName = null);

public sealed record ExtensionApplicationResponse(ApplicationDetailResponse Application, bool WasDuplicate);

public sealed record ApplicationEventResponse(
    Guid Id,
    ApplicationEventType Type,
    DateTimeOffset OccurredAt,
    Source Source,
    string? Metadata);

/// <summary>One row of an application's status history — what changed, when, and how it was
/// applied. Origin (not Source) is what the UI labels the row with: an email-driven change is
/// Source.Email whether the user confirmed it or it was applied unattended.</summary>
public sealed record ApplicationStatusHistoryResponse(
    Guid Id,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTimeOffset ChangedAt,
    string? Note,
    StatusChangeOrigin Origin,
    Source Source,
    Guid? EmailSuggestionId,
    RejectionReasonCategory? RejectionReasonCategory,
    string? RejectionReasonDetail,
    // The originating email's subject/snippet, resolved at read time when the suggestion still
    // exists. Not snapshotted onto the history row: unlike the rejection reason (which the UI
    // states as fact about the change), this is only context for "show me the email" — null is a
    // fine answer once the suggestion is gone.
    string? EmailSubject = null,
    string? EmailSnippet = null);

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public sealed record ApplicationSummaryCountsResponse(
    int Total,
    int Active,
    int Waiting,
    int Interviews,
    int Offers,
    int Rejected,
    int Ghosted);
