using Hangfire;

namespace AfterApply.Infrastructure;

/// <summary>
/// How long a job's stored arguments and exception outlive it. Hangfire expires succeeded and
/// deleted jobs after a day, but a job that exhausts its retries lands in Failed, which never
/// expires — its arguments (ids, since 2026-09-24, but ids of rows the account may since have
/// deleted) and its exception text stay in the job tables for good. Nothing here reads a failed
/// job: there is no dashboard, and the retry filter already logs the final failure. So a job that
/// runs out of attempts is deleted instead, and expires with every other finished job.
/// </summary>
public static class HangfireJobRetention
{
    /// <summary>Hangfire's own default, kept: about a day and a half of back-off.</summary>
    public const int RetryAttempts = 10;

    /// <summary>Replaces the default global retry filter. Idempotent — the integration suite builds
    /// many hosts in one process, and <see cref="GlobalJobFilters"/> is static.</summary>
    public static void Apply()
    {
        foreach (var existing in GlobalJobFilters.Filters.Where(f => f.Instance is AutomaticRetryAttribute).ToList())
        {
            GlobalJobFilters.Filters.Remove(existing.Instance);
        }

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = RetryAttempts,
            OnAttemptsExceeded = AttemptsExceededAction.Delete
        });
    }
}
