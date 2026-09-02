using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

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

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public sealed record ApplicationSummaryCountsResponse(
    int Total,
    int Active,
    int Waiting,
    int Interviews,
    int Offers,
    int Rejected,
    int Ghosted);
