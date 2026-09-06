using AfterApply.Domain.Common;

namespace AfterApply.Application.TrackedJobs.Contracts;

public sealed record CreateTrackedJobRequest(
    string CompanyName,
    string JobTitle,
    string? JobUrl,
    string? Location,
    string? Notes,
    // Rarely known this early — a job is usually tracked before there is any contact at all — but
    // carried across to the Application on conversion when it is.
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null);

public sealed record ConvertTrackedJobRequest(
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    string? Notes);

/// <summary>
/// Mobile client: it has no page DOM to scrape (unlike the browser extension), only the URL the
/// user shared/pasted. Resolving this never fails the request — see IJobLinkPreviewService.
/// </summary>
public sealed record ResolveTrackedJobLinkRequest(string JobUrl);
