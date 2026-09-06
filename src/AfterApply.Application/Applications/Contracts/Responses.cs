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
    string? JobDescriptionHtml = null);

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
