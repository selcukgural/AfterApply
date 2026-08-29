namespace AfterApply.Application.TrackedJobs.Contracts;

public sealed record TrackedJobResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string JobTitle,
    string? JobUrl,
    string? Location,
    string? Notes,
    DateTimeOffset AddedAt);

/// <summary>
/// Best-effort — either field may be null when nothing could be resolved (unsupported host,
/// unreachable, unparsable). The caller (mobile) must present these as editable, not final.
/// </summary>
public sealed record TrackedJobLinkPreviewResponse(
    string? SuggestedCompanyName,
    string? SuggestedJobTitle,
    string JobUrl);
