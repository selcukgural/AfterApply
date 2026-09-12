namespace AfterApply.Domain.JobSources;

/// <summary>
/// The per-user summary of one weekly run: how many postings the user's queries surfaced, how many
/// were delivered, and how many were left out and why. The counts are what lets the product say
/// "2 ilan zaten başvurduğunuz için çıkarıldı" — the identities of the excluded postings are
/// deliberately not kept; the user can already see their own applications.
/// </summary>
public sealed class UserJobSourceRun
{
    public Guid UserId { get; private set; }

    public int WeekKey { get; private set; }

    public DateTimeOffset RanAt { get; private set; }

    public int CandidateCount { get; private set; }

    public int DeliveredCount { get; private set; }

    public int ExcludedAppliedCount { get; private set; }

    public int ExcludedRecentlyShownCount { get; private set; }

    private UserJobSourceRun()
    {
    }

    public static UserJobSourceRun Create(Guid userId, int weekKey, DateTimeOffset now) =>
        new() { UserId = userId, WeekKey = weekKey, RanAt = now };

    public void Record(int candidateCount, int deliveredCount, int excludedAppliedCount, int excludedRecentlyShownCount,
        DateTimeOffset now)
    {
        RanAt = now;
        CandidateCount = candidateCount;
        DeliveredCount += deliveredCount;
        ExcludedAppliedCount = excludedAppliedCount;
        ExcludedRecentlyShownCount = excludedRecentlyShownCount;
    }
}
