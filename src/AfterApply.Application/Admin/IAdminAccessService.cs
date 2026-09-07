namespace AfterApply.Application.Admin;

public interface IAdminAccessService
{
    /// <summary>
    /// Whether this user id may read internal admin surfaces. Resolves the account's *current*
    /// email from the database rather than reading the token's email claim: a claim is only as
    /// fresh as the last sign-in, and the answer should follow the account, not the token.
    /// </summary>
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
}
