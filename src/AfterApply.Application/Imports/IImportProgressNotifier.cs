using AfterApply.Application.Imports.Contracts;

namespace AfterApply.Application.Imports;

/// <summary>
/// Pushes live import progress to whatever transport the host wires up (SignalR in this app).
/// The Infrastructure-layer import processing calls this; the Api layer supplies the
/// implementation, since only it has access to the SignalR hub context.
/// </summary>
public interface IImportProgressNotifier
{
    Task NotifyProgressAsync(ImportSummaryResponse status, CancellationToken cancellationToken);

    Task NotifyCompletedAsync(ImportSummaryResponse summary, CancellationToken cancellationToken);

    Task NotifyFailedAsync(ImportSummaryResponse status, CancellationToken cancellationToken);
}
