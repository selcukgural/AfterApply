using AfterApply.Domain.Applications;
using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Application.EmailIntegrations.Contracts;

public sealed record EmailSuggestionResponse(
    Guid Id,
    Guid? ApplicationId,
    string CompanyName,
    string JobTitle,
    ApplicationStatus? SuggestedStatus,
    double ConfidenceScore,
    string Subject,
    string Snippet,
    DateTimeOffset EmailReceivedAt,
    // True when ApplicationId is null: this email matched no existing Application, and
    // CompanyName/JobTitle/Location/Description come from IEmailJobExtractionProvider rather than
    // an already-persisted Application/Company — confirming this suggestion creates them.
    bool IsNewApplicationSuggestion = false,
    string? Location = null,
    string? Description = null,
    // Only set when SuggestedStatus is Rejected — see IEmailRejectionReasonExtractionProvider.
    // NotStated (not null) is the expected majority value, not an edge case.
    RejectionReasonCategory? RejectionReasonCategory = null,
    string? RejectionReasonDetail = null);

public sealed record SuggestionCountResponse(int Count);

/// <summary>A resolved (AutoApplied or Confirmed) email-derived state-change event, for the
/// Notifications screen — deliberately generic field names (Status/WasAutoApplied/IsRead) rather
/// than EmailSuggestion-specific naming, so a future iteration could merge in another notification
/// source (e.g. the existing Reminder module) without a DTO rewrite.</summary>
public sealed record EmailNotificationResponse(
    Guid Id,
    Guid? ApplicationId,
    string CompanyName,
    string JobTitle,
    ApplicationStatus? Status,
    bool WasAutoApplied,
    // True when this event was originally a "new job" suggestion (ApplicationId started null) —
    // confirming it created the Application, rather than changing an existing one's status.
    bool IsNewApplicationSuggestion,
    EmailApplicationMatchType? MatchType,
    double ConfidenceScore,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record NotificationCountResponse(int UnreadCount);

public enum ConfirmSuggestionResult
{
    NotFound,
    NoStatusToConfirm,
    Confirmed
}
