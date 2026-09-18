using AfterApply.Domain.CandidateExperiences;

namespace AfterApply.Application.CandidateExperiences.Contracts;

/// <summary>One optional category rating. A list of these rather than a dictionary keyed by
/// enum: it serialises as plain JSON and reads the same in OpenAPI.</summary>
public sealed record ExperienceCategoryRatingDto(ExperienceCategory Category, int Rating);

/// <summary>What the candidate says — a required overall rating, optional category ratings,
/// picks from <see cref="ExperienceStatementCatalogue"/> by key, and closed-list facts about the
/// process. There is no free text on purpose. One shape for create and update; the route carries
/// the company or the experience id.</summary>
public sealed record CandidateExperienceRequest(
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRatingDto>? CategoryRatings = null,
    IReadOnlyList<string>? LikedStatements = null,
    IReadOnlyList<string>? ImprovableStatements = null,
    HiringOutcome? Outcome = null,
    ProcessDuration? Duration = null,
    StageCount? Stages = null,
    IReadOnlyList<InterviewType>? InterviewTypes = null);

public sealed record CandidateExperienceListQuery(int Page = 1);

/// <summary>The admin table: an optional company-name filter, newest first.</summary>
public sealed record AdminCandidateExperienceListQuery(string? Company = null, int Page = 1);
