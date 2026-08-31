using AfterApply.Application.EmailIntegrations;

namespace AfterApply.IntegrationTests.EmailIntegrations;

/// <summary>Test double for IEmailJobExtractionProvider, registered into WebApplicationFactory's DI
/// container in place of the real OpenAI-backed implementation — lets the "unmatched forwarded
/// email creates a new-job suggestion" flow be integration-tested without a real OpenAI key.
/// Defaults to returning null (not confident) so tests must opt in to a result explicitly.</summary>
public sealed class FakeEmailJobExtractionProvider : IEmailJobExtractionProvider
{
    public EmailJobExtractionResult? Result { get; set; }

    public int CallCount { get; private set; }

    public Task<EmailJobExtractionResult?> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
