namespace AfterApply.Domain.JobSources;

/// <summary>
/// A posting handed to a user by one weekly run. This is the row everything downstream reads —
/// the weekly cap counts it, the 30-day "don't show again" rule reads it, and the scoring and
/// e-mail turns will hang their results off it. Composite key (user, posting): a posting is
/// delivered to a user at most once, ever.
/// </summary>
public sealed class UserJobSourceDelivery
{
    public Guid UserId { get; private set; }

    public Guid PostingId { get; private set; }

    public Guid QueryId { get; private set; }

    public DateTimeOffset DeliveredAt { get; private set; }

    /// <summary>ISO 8601 week the delivery counts against, as <c>yyyyWW</c> (e.g. 202637).</summary>
    public int WeekKey { get; private set; }

    /// <summary>Position in the user's list for that week, 0 first — the order the sweep chose
    /// (best source rank first, round-robin across titles), which is the order the list shows.</summary>
    public int Rank { get; private set; }

    /// <summary>The model's fit score, 0–100, or null while the posting is unscored — no
    /// description yet, no consent, the budget ran out, or the call failed.</summary>
    public int? Score { get; private set; }

    /// <summary>One or two sentences in the user's language on why the score is what it is.</summary>
    public string? ScoreSummary { get; private set; }

    /// <summary>Requirements in the posting the CV meets, as short phrases.</summary>
    public string[] MatchedCriteria { get; private set; } = [];

    /// <summary>Requirements in the posting the CV does not show.</summary>
    public string[] MissingCriteria { get; private set; } = [];

    /// <summary>The skills the posting asks for, whether or not the CV has them.</summary>
    public string[] RequiredSkills { get; private set; } = [];

    public DateTimeOffset? ScoredAt { get; private set; }

    /// <summary>Calls made for this row, successful or not. The scorer gives up after
    /// <see cref="MaxScoreAttempts"/> so one posting the model keeps choking on cannot be re-bought every week.</summary>
    public int ScoreAttempts { get; private set; }

    public const int MaxScoreAttempts = 2;

    public const int MaxSummaryLength = 600;

    public const int MaxCriteriaItems = 8;

    public const int MaxCriterionLength = 120;

    public bool CanBeScored => Score is null && ScoreAttempts < MaxScoreAttempts;

    private UserJobSourceDelivery()
    {
    }

    public void SetScore(int score, string summary, IEnumerable<string> matched, IEnumerable<string> missing,
        IEnumerable<string> requiredSkills, DateTimeOffset now)
    {
        Score = Math.Clamp(score, 0, 100);
        ScoreSummary = Truncate(summary, MaxSummaryLength);
        MatchedCriteria = Trim(matched);
        MissingCriteria = Trim(missing);
        RequiredSkills = Trim(requiredSkills);
        ScoredAt = now;
        ScoreAttempts++;
    }

    public void RecordScoringAttemptFailed() => ScoreAttempts++;

    private static string[] Trim(IEnumerable<string> items) => items
        .Where(i => !string.IsNullOrWhiteSpace(i))
        .Select(i => Truncate(i.Trim(), MaxCriterionLength))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaxCriteriaItems)
        .ToArray();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    public static UserJobSourceDelivery Create(Guid userId, Guid postingId, Guid queryId, int weekKey, int rank, DateTimeOffset now) =>
        new() { UserId = userId, PostingId = postingId, QueryId = queryId, WeekKey = weekKey, Rank = rank, DeliveredAt = now };
}
