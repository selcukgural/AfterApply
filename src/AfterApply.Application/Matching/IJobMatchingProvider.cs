using AfterApply.Domain.Matching;

namespace AfterApply.Application.Matching;

/// <summary>Port to the AI provider that scores a CV against a job description (spec §12). Lets
/// JobMatchingService be unit-tested with a fake implementation instead of calling OpenAI — the
/// real implementation (OpenAiJobMatchingProvider, Infrastructure layer) is exercised manually
/// once a real API key is configured.</summary>
public interface IJobMatchingProvider
{
    Task<JobMatchProviderResult> MatchAsync(string cvText, string jobDescription, CancellationToken cancellationToken);
}

public sealed record JobMatchProviderResult(
    int Score,
    IReadOnlyList<string> StrongMatches,
    IReadOnlyList<string> Missing,
    JobMatchRecommendation Recommendation);
