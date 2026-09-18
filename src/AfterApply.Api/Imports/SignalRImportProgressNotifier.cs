using AfterApply.Application.Imports;
using AfterApply.Application.Imports.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AfterApply.Api.Imports;

/// <summary>
/// Pushes an import's status to the uploader's browser. Best-effort by design: the client polls
/// GET /api/imports/{id} as well, so a push that does not go out costs a few seconds of latency,
/// not the import. That matters since the hub went onto the Redis backplane — a send while Redis
/// is unreachable throws, and letting that escape from inside ImportService's catch block would
/// fail the Hangfire job (and its NotifyFailedAsync, which would throw again) over a lost
/// notification.
/// </summary>
internal sealed class SignalRImportProgressNotifier(IHubContext<ImportProgressHub> hubContext, ILogger<SignalRImportProgressNotifier> logger)
    : IImportProgressNotifier
{
    public Task NotifyProgressAsync(ImportSummaryResponse status, CancellationToken cancellationToken) =>
        SendAsync(status, cancellationToken);

    public Task NotifyCompletedAsync(ImportSummaryResponse summary, CancellationToken cancellationToken) =>
        SendAsync(summary, cancellationToken);

    public Task NotifyFailedAsync(ImportSummaryResponse status, CancellationToken cancellationToken) =>
        SendAsync(status, cancellationToken);

    private async Task SendAsync(ImportSummaryResponse status, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.Group(ImportProgressHub.GroupName(status.Id))
                .SendAsync("importStatusChanged", status, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Import progress push for batch {BatchId} was not delivered; the client will pick it up by polling", status.Id);
        }
    }
}
