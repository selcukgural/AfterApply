using AfterApply.Domain.Applications;
using AfterApply.Domain.CandidateExperiences;

namespace AfterApply.Application.CandidateExperiences.Contracts;

// Three shapes, never mixed: what anyone on the internet sees, what the author sees of their
// own row, and — behind the admin gate only — the row with its author joined. The public record
// carries the quarter of the submission and nothing else about when or by whom: the company
// knows exactly whom it interviewed in a given month, so a month, a job title or an author field
// would each undo the anonymity promise on the privacy page. That promise is exactly this file —
// do not widen the public record "because it is handy".

public sealed record CandidateExperiencePublicResponse(
    Guid Id,
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRatingDto> CategoryRatings,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<InterviewType> InterviewTypes,
    /// <summary><c>yyyy-Qn</c> of the submission — quarter precision on purpose.</summary>
    string SubmittedQuarter,
    /// <summary>How many readers marked it helpful (2026-09-23). A count, never who.</summary>
    int HelpfulCount,
    /// <summary>The author tracked an application at this company here at least 14 days before
    /// writing (2026-09-24). A yes/no only — never which application or when.</summary>
    bool BackedByApplication = false);

/// <summary>The aggregate a public company page shows. <see cref="Count"/> is always real;
/// <see cref="AverageOverall"/> and <see cref="Distribution"/> follow the review summary (shown
/// from the first entry — every entry is a public card anyway); <see cref="Score"/>, the category
/// averages, the two "most picked" lists and every process statistic stay null/empty below
/// <see cref="MinimumForStats"/> — "typical duration" from one person's process is not typical.</summary>
public sealed record CandidateExperienceSummaryResponse(
    int Count,
    double? Score,
    int MinimumForStats,
    int PriorWeight,
    double? AverageOverall,
    /// <summary>Always the eight optional categories, in <see cref="ExperienceCategory"/> order.</summary>
    IReadOnlyList<ExperienceCategoryAverageResponse> Categories,
    /// <summary>How many entries gave each Overall star, index 0 = 1 star.</summary>
    IReadOnlyList<int> Distribution,
    IReadOnlyList<ExperienceStatementCountResponse> TopLiked,
    IReadOnlyList<ExperienceStatementCountResponse> TopImprovable,
    /// <summary>Only outcomes at least one entry named, in <see cref="HiringOutcome"/> order.</summary>
    IReadOnlyList<HiringOutcomeCountResponse> Outcomes,
    ProcessDuration? TypicalDuration,
    StageCount? TypicalStages,
    /// <summary>Only types at least one entry ticked, in <see cref="InterviewType"/> order.</summary>
    IReadOnlyList<InterviewTypeCountResponse> InterviewTypes,
    /// <summary>How many entries ticked a take-home assignment — the one type worth its own number.</summary>
    int TakeHomeAssignmentCount);

public sealed record ExperienceCategoryAverageResponse(ExperienceCategory Category, int Count, double? Average);

public sealed record ExperienceStatementCountResponse(string Key, int Count);

public sealed record HiringOutcomeCountResponse(HiringOutcome Outcome, int Count);

public sealed record InterviewTypeCountResponse(InterviewType Type, int Count);

public sealed record CandidateExperiencePageResponse(
    IReadOnlyList<CandidateExperiencePublicResponse> Items,
    int Total,
    int Page,
    int PageSize,
    CandidateExperienceSummaryResponse Summary);

/// <summary>The author's view of their own row: everything, including the exact dates.</summary>
public sealed record MyCandidateExperienceResponse(
    Guid Id,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRatingDto> CategoryRatings,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<InterviewType> InterviewTypes,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record ExperienceQuotaResponse(int Used, int Limit);

public sealed record MyCandidateExperiencesResponse(IReadOnlyList<MyCandidateExperienceResponse> Items, ExperienceQuotaResponse Quota);

/// <summary>The admin table's row (2026-09-18): the author's full record plus who wrote it, the
/// one surface besides the review moderation detail where an author is joined to a response.
/// Carries everything the detail modal shows, so there is no separate detail endpoint.</summary>
public sealed record AdminCandidateExperienceListItemResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    Guid AuthorUserId,
    string AuthorEmail,
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRatingDto> CategoryRatings,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<InterviewType> InterviewTypes,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

/// <summary>What a signed-in reader needs on top of the company's list: their own entry for this
/// company, if any, and how much quota is left — the contribute page's status line.</summary>
public sealed record CandidateExperienceViewerStateResponse(
    MyCandidateExperienceResponse? OwnEntry,
    ExperienceQuotaResponse Quota,
    /// <summary>The experiences at this company the caller has marked helpful — lights their buttons.</summary>
    IReadOnlyList<Guid> HelpfulMarkedExperienceIds);

/// <summary>One ended process the caller may want to rate. <paramref name="Outcome"/> is the
/// application's closing status (Rejected, Ghosted or Accepted — a withdrawal is the candidate's
/// own call and is never asked about); <paramref name="EndedAt"/> is when it moved there.</summary>
public sealed record ExperienceInviteResponse(
    Guid ApplicationId,
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    string JobTitle,
    ApplicationStatus Outcome,
    DateTimeOffset EndedAt);
