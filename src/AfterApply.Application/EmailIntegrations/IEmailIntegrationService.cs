using AfterApply.Application.EmailIntegrations.Contracts;

namespace AfterApply.Application.EmailIntegrations;

public interface IEmailIntegrationService
{
    Task<string> BuildAuthorizationUrlAsync(Guid userId, CancellationToken cancellationToken);

    Task<EmailConnectionCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken cancellationToken);

    Task<EmailConnectionStatusResponse> GetConnectionStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> DisconnectAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    Task<bool> DismissSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken);

    /// <summary>Invoked by the Hangfire recurring job; no per-request user context — scans all
    /// users' connected accounts, mirrors ReminderService.ScanAndGenerateRemindersAsync.</summary>
    Task<int> SyncAllConnectionsAsync(CancellationToken cancellationToken);
}
