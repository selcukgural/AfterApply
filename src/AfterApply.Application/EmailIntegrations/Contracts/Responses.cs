using AfterApply.Domain.Applications;

namespace AfterApply.Application.EmailIntegrations.Contracts;

public sealed record EmailConnectionStatusResponse(
    bool Connected,
    string? ProviderAccountEmail,
    DateTimeOffset? LastSyncedAt,
    bool NeedsReattention);

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
    string? Description = null);

public sealed record EmailConnectionCallbackResult(bool Succeeded, string? ErrorReason);

public enum ConfirmSuggestionResult
{
    NotFound,
    NoStatusToConfirm,
    Confirmed
}
