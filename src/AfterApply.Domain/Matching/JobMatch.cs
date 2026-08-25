using AfterApply.Domain.Common;

namespace AfterApply.Domain.Matching;

/// <summary>
/// One row per Application (unique index on ApplicationId, see JobMatchConfiguration) —
/// recomputing overwrites the previous result rather than keeping history. CvTextSnapshot and
/// JobDescription record exactly what was sent to the AI provider, so a repeat request can be
/// served from cache when neither has changed since (DECISIONS.md Sprint 8).
/// </summary>
public sealed class JobMatch : Entity
{
    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string CvTextSnapshot { get; private set; } = string.Empty;

    public string JobDescription { get; private set; } = string.Empty;

    public int Score { get; private set; }

    public IReadOnlyList<string> StrongMatches { get; private set; } = [];

    public IReadOnlyList<string> Missing { get; private set; } = [];

    public JobMatchRecommendation Recommendation { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    private JobMatch()
    {
    }

    public static JobMatch Create(Guid userId, Guid applicationId, string cvTextSnapshot, string jobDescription,
        int score, IReadOnlyList<string> strongMatches, IReadOnlyList<string> missing,
        JobMatchRecommendation recommendation, DateTimeOffset now)
    {
        return new JobMatch
        {
            UserId = userId,
            ApplicationId = applicationId,
            CvTextSnapshot = cvTextSnapshot,
            JobDescription = jobDescription,
            Score = score,
            StrongMatches = strongMatches,
            Missing = missing,
            Recommendation = recommendation,
            ComputedAt = now
        };
    }

    public bool MatchesInputs(string cvText, string jobDescription)
    {
        return CvTextSnapshot == cvText && JobDescription == jobDescription;
    }

    public void Recompute(string cvTextSnapshot, string jobDescription, int score,
        IReadOnlyList<string> strongMatches, IReadOnlyList<string> missing,
        JobMatchRecommendation recommendation, DateTimeOffset now)
    {
        CvTextSnapshot = cvTextSnapshot;
        JobDescription = jobDescription;
        Score = score;
        StrongMatches = strongMatches;
        Missing = missing;
        Recommendation = recommendation;
        ComputedAt = now;
    }
}
