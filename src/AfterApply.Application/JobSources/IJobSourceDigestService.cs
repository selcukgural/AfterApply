namespace AfterApply.Application.JobSources;

/// <summary>
/// The Monday "N postings are ready" e-mail. The sweep calls <see cref="EnqueueWeekAsync"/> once
/// scoring is done; that queues one <see cref="SendAsync"/> per user as its own background job,
/// so a bad minute at the e-mail provider is retried per user by Hangfire and never fails the
/// sweep. One digest per user per week, stamped on the run row, however many times either runs.
/// </summary>
public interface IJobSourceDigestService
{
    Task<int> EnqueueWeekAsync(int weekKey, CancellationToken cancellationToken);

    Task SendAsync(Guid userId, int weekKey, CancellationToken cancellationToken);
}
