using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

/// <summary>
/// The model call behind one fit score. One request, one posting: the CV is sent with every
/// posting rather than once per user because the shape stays stateless and a failure costs one
/// posting, not the week. Throws on a provider error; the scorer catches, counts the attempt and
/// moves on.
/// </summary>
public interface IJobFitScoringProvider
{
    string Model { get; }

    Task<JobFitScoringResult?> ScoreAsync(JobFitScoringRequest request, CancellationToken cancellationToken);
}

/// <summary>Runs after the sweep has delivered and fetched descriptions: scores every unscored
/// delivery of the week for users who consented, inside the daily call ceiling and the monthly
/// budget. Idempotent — a scored row is never scored again.</summary>
public interface IJobFitScoringService
{
    Task<int> ScoreWeekAsync(int weekKey, CancellationToken cancellationToken);
}

/// <summary>The text of a user's default CV, read from storage and extracted on demand; never
/// cached and never written anywhere. Null when the user has no CV or it cannot be read.</summary>
public interface IUserCvTextReader
{
    Task<string?> ReadDefaultCvTextAsync(Guid userId, CancellationToken cancellationToken);
}
