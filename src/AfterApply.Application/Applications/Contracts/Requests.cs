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
    string? HrLinkedInUrl = null,
    // Which stored CV was sent with this application. Optional; must be one of the caller's own
    // CVs, which the service checks — an id from a request body is never proof of ownership.
    Guid? CvDocumentId = null);

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
    string? CompanyKariyerNetUrl = null,
    // The job poster from LinkedIn's hiring-team card, when the posting has one (it is opt-in, so
    // most do not) — scoped to that card specifically, never to any profile link on the page, since
    // a job page also lists unrelated alumni and network suggestions. HrEmail is only ever an
    // address spelled out in the posting body, which in practice almost never happens; both sites
    // route applications through their own funnel. Every one of these is shown as an editable field
    // in the popup before submitting, so a wrong guess is the user's to correct, not a silent write.
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null);

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
    string? HrLinkedInUrl = null,
    Guid? CvDocumentId = null);

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

/// <summary>How the company view orders the groups it shows. Deliberately not
/// <see cref="ApplicationListSortBy"/>: half of that enum (job title, status, applied date) names a
/// property of a single application, which a company holding several of them does not have.</summary>
public enum CompanyGroupSortBy
{
    /// <summary>The most recent UpdatedAt across the company's matching applications.</summary>
    LastActivity,
    ApplicationCount,
    CompanyName
}

public sealed record GetApplicationsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    ApplicationStatus? Status = null,
    // Narrows the list to one company. Added for the company view's "show all N at this company"
    // link, and free for every bulk operation too: FilteredApplications is the single place that
    // resolves a filter into rows, so a filter-shaped bulk selection inherits it.
    Guid? CompanyId = null,
    ApplicationListSortBy SortBy = ApplicationListSortBy.AppliedAt,
    SortDirection SortDirection = SortDirection.Descending);

/// <summary>
/// The company view's query. Same row filter as <see cref="GetApplicationsQuery"/> — the two views
/// must agree about which applications exist — but the unit of paging is the company, so
/// <c>PageSize</c> counts companies, not applications.
/// </summary>
public sealed record GetGroupedApplicationsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    ApplicationStatus? Status = null,
    CompanyGroupSortBy SortBy = CompanyGroupSortBy.LastActivity,
    SortDirection SortDirection = SortDirection.Descending);

/// <summary>
/// Which applications a bulk operation applies to. Exactly one of the two is set, and the
/// distinction is not cosmetic: <paramref name="Ids"/> is a set the user could see and count on
/// screen, while <paramref name="AllMatching"/> is a filter whose result the server resolves —
/// including rows on pages the user never opened. The second form is what "Delete All" rides on,
/// and it is why <c>ExpectedCount</c> exists on the requests below.
/// </summary>
public sealed record BulkSelection(
    IReadOnlyList<Guid>? Ids = null,
    BulkFilterSelection? AllMatching = null);

/// <summary>The list's own filter, repeated back so the server resolves exactly the set the user
/// was looking at. Deliberately only the fields that narrow the row set — paging and sorting cannot
/// change which applications match, so accepting them would only invite them to drift.</summary>
/// <param name="CompanyId">Set when the list the user was looking at was narrowed to one company —
/// the company view's "show the remaining N at this company" link does exactly that. Without it,
/// "select all matching" on that screen would resolve to every company's rows instead of the one on
/// screen, which is the widest possible way to get a destructive operation wrong.</param>
public sealed record BulkFilterSelection(string? Search = null, ApplicationStatus? Status = null,
    Guid? CompanyId = null);

/// <param name="ExpectedCount">How many applications the user was told they were acting on.
/// Required for an <c>AllMatching</c> selection and refused when the count no longer holds, so a
/// row that appeared between the screen being drawn and the button being pressed cannot be swept
/// into an operation nobody agreed to. Ignored for an explicit id list, where the set is already
/// exact.</param>
public sealed record BulkChangeStatusRequest(
    BulkSelection Selection,
    ApplicationStatus NewStatus,
    string? Note = null,
    int? ExpectedCount = null);

/// <summary>
/// Puts back what a bulk status change did. Each entry carries the status the client last saw so
/// the server can skip anything that moved on since — an undo must never overwrite a decision the
/// user made after the change it is undoing.
/// </summary>
public sealed record UndoBulkStatusRequest(IReadOnlyList<UndoBulkStatusEntry> Entries);

public sealed record UndoBulkStatusEntry(Guid ApplicationId, ApplicationStatus ExpectedStatus, ApplicationStatus RevertTo);

/// <param name="ExpectedCount">As on <see cref="BulkChangeStatusRequest"/> — and it matters more
/// here, because this deletion is permanent.</param>
public sealed record BulkDeleteRequest(BulkSelection Selection, int? ExpectedCount = null);
