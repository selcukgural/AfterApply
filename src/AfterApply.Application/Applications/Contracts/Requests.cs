using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Application.Applications.Contracts;

public sealed record CreateApplicationRequest(
    string CompanyName,
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    Source? Source,
    string? Notes,
    // The recruiter/hiring contact, typed by the user. Neither job site publishes an HR email
    // (measured 2026-09-06: 0 of 35 kariyer.net postings carried one, and LinkedIn never shows the
    // poster's address), so manual entry is the primary way this ever gets filled.
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null);

public sealed record CreateFromExtensionRequest(
    string CompanyName,
    string JobTitle,
    string JobUrl,
    string? Location,
    string? Description,
    DateTimeOffset? PublishedAt,
    // Allow-listed HTML captured by the extension for formatted display (spec §11 follow-up) —
    // untrusted content regardless of the extension's own sanitization; re-sanitized again with
    // DOMPurify before ever rendering (see web's JobDescriptionCard).
    string? DescriptionHtml = null,
    // The LinkedIn company page URL, read from the company anchor's href while scraping the job
    // posting (never derived from CompanyName server-side). Feeds CompanyResolver's LinkedInUrl
    // backfill and, via CompanyEnrichmentService, background enrichment of
    // Website/Industry/Country. Validated against an https://(www.)linkedin.com allow-list
    // (CreateFromExtensionRequestValidator) since it's later fetched server-side — never trust it
    // as safe just because it round-tripped through the client.
    string? CompanyLinkedInUrl = null,
    // kariyer.net's counterpart, read from the job posting's own company anchor
    // (a[data-test="company-name"]). Feeds the same CompanyResolver backfill and the same
    // background enrichment, and is allow-listed the same way — it is fetched server-side too.
    // A LinkedIn posting never carries one and a kariyer.net posting never carries the LinkedIn
    // one, so at most one of the pair is ever set on a given submission.
    string? CompanyKariyerNetUrl = null);

public sealed record UpdateApplicationRequest(
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    string? Notes,
    // Sent on every save, so omitting one clears it — this is the edit form, and a user emptying
    // the field means they want it gone.
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null);

// Note is the user's own text and nothing else, and there is deliberately no Source/Origin
// here: provenance is decided by the code path that handles the change, never by the caller.
// The endpoint always records StatusChangeOrigin.Manual; internal callers (email suggestions,
// imports) go through IApplicationService's StatusChangeContext overload instead.
public sealed record ChangeStatusRequest(ApplicationStatus NewStatus, string? Note, DateTimeOffset? ChangedAt);

public sealed record CreateEventRequest(
    ApplicationEventType Type,
    DateTimeOffset? OccurredAt,
    Source? Source,
    string? Metadata);

public enum ApplicationListSortBy
{
    AppliedAt,
    CompanyName,
    JobTitle,
    Status,
    UpdatedAt
}

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record GetApplicationsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    ApplicationStatus? Status = null,
    ApplicationListSortBy SortBy = ApplicationListSortBy.AppliedAt,
    SortDirection SortDirection = SortDirection.Descending);
