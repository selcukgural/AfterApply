using AfterApply.Application.Imports;
using AfterApply.Application.Imports.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace AfterApply.Api.Imports;

internal sealed class SignalRImportProgressNotifier(IHubContext<ImportProgressHub> hubContext) : IImportProgressNotifier
{
    public Task NotifyProgressAsync(ImportSummaryResponse status, CancellationToken cancellationToken) =>
        Send(status, cancellationToken);

    public Task NotifyCompletedAsync(ImportSummaryResponse summary, CancellationToken cancellationToken) =>
        Send(summary, cancellationToken);

    public Task NotifyFailedAsync(ImportSummaryResponse status, CancellationToken cancellationToken) =>
        Send(status, cancellationToken);

    private Task Send(ImportSummaryResponse status, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(ImportProgressHub.GroupName(status.Id))
            .SendAsync("importStatusChanged", status, cancellationToken);
}
