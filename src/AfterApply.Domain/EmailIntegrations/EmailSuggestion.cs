using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.EmailIntegrations;

public sealed class EmailSuggestion : Entity
{
    public Guid UserId { get; private set; }

    public Guid EmailConnectionId { get; private set; }

    /// <summary>Null means this is a "new job" suggestion (see CreateForNewJob) — the forwarded
    /// email didn't match any existing Application, but carried enough signal + extractable detail
    /// to propose creating one. Non-null is the original "update this Application's status"
    /// suggestion.</summary>
    public Guid? ApplicationId { get; private set; }

    public string ProviderMessageId { get; private set; } = string.Empty;

    public string? ProviderThreadId { get; private set; }

    public ApplicationStatus? SuggestedStatus { get; private set; }

    public double ConfidenceScore { get; private set; }

    public string MatchedRule { get; private set; } = string.Empty;

    public string? SenderDomain { get; private set; }

    public string? Subject { get; private set; }

    public string? Snippet { get; private set; }

    /// <summary>Only set on a "new job" suggestion (ApplicationId is null) — the company name the
    /// extraction provider read from the email, shown for review before ConfirmSuggestionAsync
    /// creates the Company/Application from it.</summary>
    public string? ExtractedCompanyName { get; private set; }

    public string? ExtractedJobTitle { get; private set; }

    public string? ExtractedLocation { get; private set; }

    public string? ExtractedDescription { get; private set; }

    /// <summary>Only set when SuggestedStatus is Rejected — see
    /// IEmailRejectionReasonExtractionProvider. NotStated (not null) is the expected majority
    /// outcome, not an edge case; these three stay null together when the extraction step didn't
    /// run at all (e.g. status isn't Rejected).</summary>
    public RejectionReasonCategory? RejectionReasonCategory { get; private set; }

    public string? RejectionReasonDetail { get; private set; }

    public double? RejectionReasonConfidence { get; private set; }

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
        DateTimeOffset now, string? subject = null, string? snippet = null,
        RejectionReasonCategory? rejectionReasonCategory = null, string? rejectionReasonDetail = null,
        double? rejectionReasonConfidence = null)
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
            Subject = subject,
            Snippet = snippet,
            RejectionReasonCategory = rejectionReasonCategory,
            RejectionReasonDetail = rejectionReasonDetail,
            RejectionReasonConfidence = rejectionReasonConfidence,
            EmailReceivedAt = emailReceivedAt,
            Status = EmailSuggestionStatus.Pending,
            CreatedAt = now
        };
    }

    /// <summary>A forwarded email that matched no existing Application, but carried both a
    /// classifiable status signal (or "StillWaiting") and confidently-extracted company/job-title
    /// detail — see EmailForwardingService. ApplicationId stays null until ConfirmSuggestionAsync
    /// creates the Application from the Extracted* fields.</summary>
    public static EmailSuggestion CreateForNewJob(Guid userId, Guid emailConnectionId,
        string providerMessageId, ApplicationStatus? suggestedStatus, double confidenceScore,
        string matchedRule, string? senderDomain, DateTimeOffset emailReceivedAt, DateTimeOffset now,
        string? subject, string? snippet, string extractedCompanyName, string extractedJobTitle,
        string? extractedLocation, string? extractedDescription,
        RejectionReasonCategory? rejectionReasonCategory = null, string? rejectionReasonDetail = null,
        double? rejectionReasonConfidence = null)
    {
        return new EmailSuggestion
        {
            UserId = userId,
            EmailConnectionId = emailConnectionId,
            ApplicationId = null,
            ProviderMessageId = providerMessageId,
            ProviderThreadId = null,
            SuggestedStatus = suggestedStatus,
            ConfidenceScore = confidenceScore,
            MatchedRule = matchedRule,
            SenderDomain = senderDomain,
            Subject = subject,
            Snippet = snippet,
            ExtractedCompanyName = extractedCompanyName,
            ExtractedJobTitle = extractedJobTitle,
            ExtractedLocation = extractedLocation,
            ExtractedDescription = extractedDescription,
            RejectionReasonCategory = rejectionReasonCategory,
            RejectionReasonDetail = rejectionReasonDetail,
            RejectionReasonConfidence = rejectionReasonConfidence,
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
