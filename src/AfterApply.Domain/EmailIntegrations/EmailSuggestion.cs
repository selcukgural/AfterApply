using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.EmailIntegrations;

public sealed class EmailSuggestion : Entity
{
    public Guid UserId { get; private set; }

    public Guid EmailConnectionId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string ProviderMessageId { get; private set; } = string.Empty;

    public string? ProviderThreadId { get; private set; }

    public ApplicationStatus? SuggestedStatus { get; private set; }

    public double ConfidenceScore { get; private set; }

    public string MatchedRule { get; private set; } = string.Empty;

    public string? SenderDomain { get; private set; }

    public DateTimeOffset EmailReceivedAt { get; private set; }

    public EmailSuggestionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    private EmailSuggestion()
    {
    }

    public static EmailSuggestion Create(Guid userId, Guid emailConnectionId, Guid applicationId,
        string providerMessageId, string? providerThreadId, ApplicationStatus? suggestedStatus,
        double confidenceScore, string matchedRule, string? senderDomain, DateTimeOffset emailReceivedAt,
        DateTimeOffset now)
    {
        return new EmailSuggestion
        {
            UserId = userId,
            EmailConnectionId = emailConnectionId,
            ApplicationId = applicationId,
            ProviderMessageId = providerMessageId,
            ProviderThreadId = providerThreadId,
            SuggestedStatus = suggestedStatus,
            ConfidenceScore = confidenceScore,
            MatchedRule = matchedRule,
            SenderDomain = senderDomain,
            EmailReceivedAt = emailReceivedAt,
            Status = EmailSuggestionStatus.Pending,
            CreatedAt = now
        };
    }

    public void Confirm(DateTimeOffset now)
    {
        Status = EmailSuggestionStatus.Confirmed;
        ResolvedAt = now;
    }

    public void Dismiss(DateTimeOffset now)
    {
        Status = EmailSuggestionStatus.Dismissed;
        ResolvedAt = now;
    }
}
