namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>
/// One Gmail signal between its arrival and its processing. The signal carries a sender address, a
/// subject and a snippet of someone's email; enqueued as a job argument, that text would sit in
/// Hangfire's tables for as long as the job row lives — a failed job's until someone deletes it,
/// account deletion or not. Here it is a row that cascades with the account, is deleted as soon as
/// the job has used it, and is purged after <see cref="EmailForwardingService.PendingSignalRetention"/>
/// if the job never succeeds. The job itself only carries this row's id.
/// </summary>
public sealed class PendingEmailSignal
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    /// <summary>The request as the extension sent it, serialized.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private PendingEmailSignal()
    {
    }

    public static PendingEmailSignal Create(Guid userId, string payload, DateTimeOffset now) =>
        new() { UserId = userId, Payload = payload, CreatedAt = now };
}
