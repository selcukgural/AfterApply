namespace AfterApply.Application.JobLiveness;

/// <summary>
/// The daily look at whether the postings people are still waiting on are still up. Sets
/// <c>Job.ClosedAt</c> from what each site itself says; see <c>PostingLivenessRules</c>.
/// </summary>
public interface IJobLivenessService
{
    Task RunAsync(CancellationToken cancellationToken);
}
