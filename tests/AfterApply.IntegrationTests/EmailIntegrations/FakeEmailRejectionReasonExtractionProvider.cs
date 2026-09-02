using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.IntegrationTests.EmailIntegrations;

/// <summary>Test double for IEmailRejectionReasonExtractionProvider, registered into
/// WebApplicationFactory's DI container in place of the real OpenAI-backed implementation.
/// Defaults to NotStated (the real provider's own expected majority outcome — see the mailbox
/// audit in DECISIONS.md) so tests must opt in to a stated reason explicitly.</summary>
public sealed class FakeEmailRejectionReasonExtractionProvider : IEmailRejectionReasonExtractionProvider
{
    public EmailRejectionReasonExtractionResult Result { get; set; } =
        new(RejectionReasonCategory.NotStated, Detail: null, Confidence: 0);

    public int CallCount { get; private set; }

    public Task<EmailRejectionReasonExtractionResult> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
