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

/// <summary>What the extension's "Apply later" did. Deliberately no record in it: the extension
/// token is a narrow, long-lived credential, and the popup only needs to say which of the three
/// happened — never to read back a stored row (see PersonalAccessTokenScope.Extension).</summary>
public sealed record ExtensionTrackedJobResponse(ExtensionTrackedJobOutcome Outcome);

public enum ExtensionTrackedJobOutcome
{
    Saved,

    /// <summary>This URL is already saved for later — nothing was written.</summary>
    AlreadySaved,

    /// <summary>This URL is already an application. Saving it for later as well would put the
    /// same posting in two lists, one of which says "not applied yet".</summary>
    AlreadyApplied
}
