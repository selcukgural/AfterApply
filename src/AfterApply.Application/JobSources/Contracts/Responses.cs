using AfterApply.Domain.JobSources;

namespace AfterApply.Application.JobSources.Contracts;

public sealed record JobSourceProfileResponse(
    IReadOnlyList<string> Titles,
    string Location,
    bool RemoteOnly,
    bool Enabled,
    DateTimeOffset UpdatedAt);

/// <summary>A delivered posting as the list shows it — the card's fields plus what the detail
/// fetch added, minus the description, which is the detail endpoint's.</summary>
public sealed record JobSourcePostingSummaryResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string? CompanyProfileUrl,
    string? Location,
    DateOnly? PostedAt,
    string Url,
    string? Seniority,
    string? EmploymentType,
    DateTimeOffset DeliveredAt,
    int WeekKey);

public sealed record JobSourcePostingDetailResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string? CompanyProfileUrl,
    string? Location,
    DateOnly? PostedAt,
    string Url,
    string? Description,
    string? Seniority,
    string? EmploymentType,
    string? JobFunction,
    string? Industries,
    DateTimeOffset DeliveredAt,
    int WeekKey);

/// <summary>The run summary the list carries so the UI can say what was left out and why.</summary>
public sealed record JobSourceRunResponse(
    int WeekKey,
    DateTimeOffset RanAt,
    int CandidateCount,
    int DeliveredCount,
    int ExcludedAppliedCount,
    int ExcludedRecentlyShownCount);

public sealed record JobSourceDeliveriesResponse(
    IReadOnlyList<JobSourcePostingSummaryResponse> Items,
    JobSourceRunResponse? Run);

public sealed record UserJobSourceSettingsResponse(Guid UserId, int? WeeklyPostingLimit, int EffectiveWeeklyPostingLimit);

/// <summary>Admin view of the source budget: today's request count against the ceiling, and
/// whether the sweep is currently stopped by a block.</summary>
public sealed record JobSourceUsageResponse(
    int RequestsToday,
    int MaxRequestsPerDay,
    DateTimeOffset? LastBlockedAt,
    DateTimeOffset? CooldownUntil,
    int PostingCount,
    int ActiveQueryCount);

public sealed record ProEntitlementResponse(Guid UserId, DateTimeOffset ActiveUntil, string Source, DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt, bool IsActive);

/// <summary>What one search page carried, already parsed. The source's HTML never leaves the client.</summary>
public sealed record JobSourceCard(
    string ExternalId,
    string Title,
    string CompanyName,
    string? CompanyProfileUrl,
    string? Location,
    DateOnly? PostedAt,
    string Url);

public sealed record JobSourceDetail(
    string? Description,
    string? Seniority,
    string? EmploymentType,
    string? JobFunction,
    string? Industries);
