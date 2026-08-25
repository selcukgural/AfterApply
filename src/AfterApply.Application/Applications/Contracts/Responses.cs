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
    // Only set when this Application is linked to a Job with a captured description (currently
    // only the browser extension's "I Applied" flow populates Job.Description — Sprint 9). Lets
    // the AI Job Matching panel (Sprint 8) pre-fill instead of requiring the user to paste the
    // job description by hand when it was already captured at creation time.
    string? JobDescription = null,
    // Allow-listed HTML for a formatted, read-only display of the same description — untrusted
    // content, the frontend re-sanitizes with DOMPurify before ever rendering it (see
    // JobDescriptionCard). Null whenever JobDescription is (no linked Job, or a Job predating
    // this field).
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
