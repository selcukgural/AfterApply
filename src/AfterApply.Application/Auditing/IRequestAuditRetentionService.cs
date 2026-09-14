namespace AfterApply.Application.Auditing;

public interface IRequestAuditRetentionService
{
    /// <summary>Deletes request-audit rows that belong to no account and are older than the
    /// configured retention window. Rows tied to an account are not touched here — they leave
    /// with the account.</summary>
    Task<int> PurgeAnonymousAsync(CancellationToken cancellationToken);
}
