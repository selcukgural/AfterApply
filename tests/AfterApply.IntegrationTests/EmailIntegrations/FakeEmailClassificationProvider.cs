using AfterApply.Application.EmailIntegrations;

namespace AfterApply.IntegrationTests.EmailIntegrations;

/// <summary>Test double for IEmailClassificationProvider, registered in place of the real
/// OpenAI-backed implementation — lets the RuleBasedEmailClassifier "NoMatch" fallback be exercised
/// deterministically, without a real OpenAI key, whenever a test's fixture text doesn't already
/// match one of RuleBasedEmailClassifier's own phrases.</summary>
public sealed class FakeEmailClassificationProvider : IEmailClassificationProvider
{
    public EmailClassificationResult Result { get; set; } = new(null, 0, "Llm:NoSignal");

    public Task<EmailClassificationResult> ClassifyAsync(string subject, string snippet, CancellationToken cancellationToken) =>
        Task.FromResult(Result);
}
