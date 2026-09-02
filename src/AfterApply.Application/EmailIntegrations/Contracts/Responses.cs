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

public sealed record InboundAddressResponse(
    string Address,
    // Non-null only while Gmail's own forwarding-confirmation email is pending acknowledgement —
    // see EmailConnection.SetGmailConfirmation.
    string? GmailConfirmationCode,
    string? GmailConfirmationLink,
    DateTimeOffset? GmailConfirmationReceivedAt);

public enum ConfirmSuggestionResult
{
    NotFound,
    NoStatusToConfirm,
    Confirmed
}
