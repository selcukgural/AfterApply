using AfterApply.Domain.Matching;

namespace AfterApply.Application.Matching.Contracts;

public sealed record CandidateProfileResponse(string CvText, DateTimeOffset UpdatedAt);

public sealed record JobMatchResponse(
    Guid ApplicationId,
    int Score,
    IReadOnlyList<string> StrongMatches,
    IReadOnlyList<string> Missing,
    JobMatchRecommendation Recommendation,
    DateTimeOffset ComputedAt);
