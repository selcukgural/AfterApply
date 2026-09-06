namespace AfterApply.Application.TrackedJobs.Contracts;

public sealed record TrackedJobResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    // Same read-time resolution as ApplicationDetailResponse's pair — see the comment there.
    string? CompanyWebsite,
    string? CompanyLinkedInUrl,
    string JobTitle,
    string? JobUrl,
    string? Location,
    string? Notes,
    DateTimeOffset AddedAt,
    string? HrName = null,
    string? HrEmail = null,
    string? HrLinkedInUrl = null);

/// <summary>
/// Best-effort — either field may be null when nothing could be resolved (unsupported host,
/// unreachable, unparsable). The caller (mobile) must present these as editable, not final.
/// </summary>
public sealed record TrackedJobLinkPreviewResponse(
    string? SuggestedCompanyName,
    string? SuggestedJobTitle,
    string JobUrl);
