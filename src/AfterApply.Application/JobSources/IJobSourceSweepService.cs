namespace AfterApply.Application.JobSources;

/// <summary>The weekly run. Only Hangfire calls it; there is no endpoint that triggers it.</summary>
public interface IJobSourceSweepService
{
    Task SweepAsync(CancellationToken cancellationToken);
}
