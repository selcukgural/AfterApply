using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Application.EmailIntegrations;

public sealed record EmailRejectionReasonExtractionResult(RejectionReasonCategory Category, string? Detail, double Confidence);

/// <summary>Port to the LLM that reads why a rejection happened, if the email says so — only tried
/// after ClassifyAsync lands on ApplicationStatus.Rejected (see EmailForwardingService). A real
/// mailbox audit (2026-09-02, see DECISIONS.md) found most rejection emails are boilerplate with no
/// individualized reason at all, so RejectionReasonCategory.NotStated is the expected majority
/// result, not a fallback for a broken call — this always returns a result (never null), unlike
/// IEmailJobExtractionProvider.</summary>
public interface IEmailRejectionReasonExtractionProvider
{
    Task<EmailRejectionReasonExtractionResult> ExtractAsync(string subject, string snippet, CancellationToken cancellationToken);
}
