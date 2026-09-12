namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>What <c>GET /api/job-search/settings</c> returns: the values that will actually apply
/// to the caller's next search, the overrides they (or an admin) have set, and the global
/// defaults those overrides fall back to.</summary>
public sealed record JobSearchSettingsResponse(
    EffectiveJobSearchSettings Effective,
    JobSearchUserOverridesResponse? UserOverrides,
    JobSearchGlobalDefaults GlobalDefaults);

/// <summary>The stored per-user row, nulls included — null means "not overridden".</summary>
public sealed record JobSearchUserOverridesResponse(
    string? DefaultCountry,
    string? DefaultLanguage,
    string? DefaultLocation,
    JobSearchDatePosted? DefaultDatePosted,
    bool? DefaultWorkFromHome,
    int? PerUserDailyCredits,
    int? MaxPagesPerSearch,
    int? MaxJobIdsPerDetails,
    DateTimeOffset UpdatedAt);

/// <summary>Answer to <c>GET /api/job-search/usage</c>: the caller's own day against their daily
/// limit, the whole product's month against the shared quota, and the provider's last word on
/// what is left (from its <c>x-ratelimit-requests-remaining</c> header, when one was seen).</summary>
public sealed record JobSearchUsageResponse(
    int DailyCreditsUsed,
    int DailyCreditLimit,
    int MonthlyCreditsUsed,
    int MonthlyCreditLimit,
    DateTimeOffset MonthlyWindowStart,
    int? UpstreamRequestsRemaining,
    DateTimeOffset? UpstreamObservedAt);
