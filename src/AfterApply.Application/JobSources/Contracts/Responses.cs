using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;

namespace AfterApply.Application.JobSources.Contracts;

public sealed record JobSourceProfileResponse(
    IReadOnlyList<string> Titles,
    string Location,
    bool RemoteOnly,
    bool Enabled,
    int MinScore,
    DateTimeOffset? AiScoringConsentAcceptedAt,
    bool EmailDigest,
    DateTimeOffset UpdatedAt);

/// <summary>A delivered posting as the list shows it — the card's fields plus what the detail
/// fetch added, minus the description, which is the detail endpoint's.</summary>
public sealed record JobSourcePostingSummaryResponse(
    Guid Id,
    Source Source,
    string Title,
    string CompanyName,
    string? CompanyProfileUrl,
    string? Location,
    DateOnly? PostedAt,
    string Url,
    string? Seniority,
    string? EmploymentType,
    DateTimeOffset DeliveredAt,
    int WeekKey,
    // Null until scored: no description yet, no consent, or the scorer has not reached it.
    int? Score,
    string? ScoreSummary);

public sealed record JobSourcePostingDetailResponse(
    Guid Id,
    Source Source,
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
    int WeekKey,
    int? Score,
    string? ScoreSummary,
    IReadOnlyList<string> MatchedCriteria,
    IReadOnlyList<string> MissingCriteria,
    IReadOnlyList<string> RequiredSkills,
    DateTimeOffset? ScoredAt);

/// <summary>The run summary the list carries so the UI can say what was left out and why.</summary>
public sealed record JobSourceRunResponse(
    int WeekKey,
    DateTimeOffset RanAt,
    int CandidateCount,
    int DeliveredCount,
    int ExcludedAppliedCount,
    int ExcludedRecentlyShownCount,
    // How many of the week's deliveries were scored, and how many the user's MinScore hides.
    // Computed when read, so moving the threshold changes them at once.
    int ScoredCount,
    int HiddenBelowMinScoreCount);

public sealed record JobSourceDeliveriesResponse(
    IReadOnlyList<JobSourcePostingSummaryResponse> Items,
    JobSourceRunResponse? Run);

/// <summary>What the page needs before it can decide which state to show: paying or not, and
/// whether there is a CV to score against. The profile itself is its own endpoint.</summary>
public sealed record JobSourceStatusResponse(
    bool IsPro,
    DateTimeOffset? ProActiveUntil,
    bool HasCv,
    string? CvFileName,
    bool HasProfile,
    /// <summary>Whether the user closed the dashboard's announcement of this feature; the card
    /// stays away once true.</summary>
    bool AnnouncementDismissed);

public sealed record UserJobSourceSettingsResponse(Guid UserId, int? WeeklyPostingLimit, int EffectiveWeeklyPostingLimit);

/// <summary>Admin view of the source budget: today's request count against the ceiling, and
/// whether the sweep is currently stopped by a block.</summary>
public sealed record JobSourceUsageResponse(
    int RequestsToday,
    int MaxRequestsPerDay,
    DateTimeOffset? LastBlockedAt,
    DateTimeOffset? CooldownUntil,
    int PostingCount,
    int ActiveQueryCount,
    JobFitScoringUsageResponse Scoring,
    // The same three numbers per source — the ceiling and the cooldown are per site.
    IReadOnlyList<JobSourcePerSourceUsageResponse> Sources);

public sealed record JobSourcePerSourceUsageResponse(
    Source Source,
    bool Enabled,
    int RequestsToday,
    DateTimeOffset? LastBlockedAt,
    DateTimeOffset? CooldownUntil);

/// <summary>The scoring side of the same admin view: today's model calls against the ceiling, and
/// the month's tokens with the cost they come to at the configured prices.</summary>
public sealed record JobFitScoringUsageResponse(
    int CallsToday,
    int MaxCallsPerDay,
    long InputTokensThisMonth,
    long OutputTokensThisMonth,
    decimal EstimatedCostUsdThisMonth,
    decimal MonthlyBudgetUsd);

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
    string Url,
    // kariyer.net prints the work model on the card ("Uzaktan", "Hibrit", "İş Yerinde"); the
    // remote-only filter is applied on it because the site has no URL parameter for it.
    string? WorkModel = null);

/// <summary>One page of a search: the cards, and whether the source says there is another
/// page — LinkedIn by a full page, kariyer.net by a "next" link.</summary>
public sealed record JobSourceSearchPage(IReadOnlyList<JobSourceCard> Cards, bool HasMore);

public sealed record JobSourceDetail(
    string? Description,
    string? Seniority,
    string? EmploymentType,
    string? JobFunction,
    string? Industries);

/// <summary>What the scoring model is given for one (CV, posting) pair. The CV text is the user's
/// default CV as extracted; the posting fields are what the sweep stored.</summary>
public sealed record JobFitScoringRequest(
    string CvText,
    string Title,
    string CompanyName,
    string? Location,
    string Description,
    string? Seniority,
    string? EmploymentType,
    string Locale);

/// <summary>The model's answer, plus what it cost. Sanitised by <see cref="JobFitScores"/> before
/// it is stored — a provider returns what it got.</summary>
public sealed record JobFitScoringResult(
    int Score,
    string Summary,
    IReadOnlyList<string> MatchedCriteria,
    IReadOnlyList<string> MissingCriteria,
    IReadOnlyList<string> RequiredSkills,
    int InputTokens,
    int OutputTokens);
