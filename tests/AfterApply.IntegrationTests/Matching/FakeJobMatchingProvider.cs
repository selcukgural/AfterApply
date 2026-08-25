using AfterApply.Application.Matching;
using AfterApply.Domain.Matching;

namespace AfterApply.IntegrationTests.Matching;

/// <summary>Test double for IJobMatchingProvider, registered into WebApplicationFactory's DI
/// container in place of the real OpenAiJobMatchingProvider — lets the cache/persist behavior of
/// JobMatchingService be integration-tested without a real OpenAI API key. CallCount lets tests
/// assert the provider is NOT called again when JobMatchingService serves a result from cache.</summary>
public sealed class FakeJobMatchingProvider : IJobMatchingProvider
{
    public int CallCount { get; private set; }

    public JobMatchProviderResult Result { get; set; } =
        new(80, ["C#", ".NET"], ["React"], JobMatchRecommendation.Apply);

    public Task<JobMatchProviderResult> MatchAsync(string cvText, string jobDescription, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
