using AfterApply.Domain.Applications;

namespace AfterApply.Application.EmailIntegrations.Contracts;

public sealed record EmailConnectionStatusResponse(
    bool Connected,
    string? ProviderAccountEmail,
    DateTimeOffset? LastSyncedAt,
    bool NeedsReattention);

public sealed record EmailSuggestionResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    ApplicationStatus? SuggestedStatus,
    double ConfidenceScore,
    string Subject,
    string Snippet,
    DateTimeOffset EmailReceivedAt);

public sealed record EmailConnectionCallbackResult(bool Succeeded, string? ErrorReason);

public enum ConfirmSuggestionResult
{
    NotFound,
    NoStatusToConfirm,
    Confirmed
}
