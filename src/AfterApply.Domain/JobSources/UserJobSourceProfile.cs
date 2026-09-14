using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSources;

/// <summary>
/// What one user wants the weekly sweep to look for: up to <see cref="MaxTitles"/> job titles in
/// one location, optionally remote-only. Each title resolves to a shared <see cref="JobSourceQuery"/>
/// through <see cref="UserJobSourceProfileQuery"/>; the profile itself is the user's, and goes with
/// the account (cascade).
/// </summary>
public sealed class UserJobSourceProfile : Entity
{
    public const int MaxTitles = 3;

    public const int MinScoreFloor = 0;

    public const int MinScoreCeiling = 100;

    public Guid UserId { get; private set; }

    public string Location { get; private set; } = string.Empty;

    public bool RemoteOnly { get; private set; }

    public bool Enabled { get; private set; }

    /// <summary>Fit score (0–100) below which a delivered posting is hidden from the user's list.
    /// Applied when the list is read, not when the sweep delivers, so the user can move it and see
    /// more or fewer of the same week without anything being re-fetched or re-scored. 0 hides nothing.</summary>
    public int MinScore { get; private set; }

    /// <summary>
    /// When the user agreed (KVKK m.6, explicit) to their default CV's text being sent to the
    /// scoring model — the one thing this feature does that the CV upload consent did not cover
    /// (the CVs page promises the files are never analysed by an AI service). Stamped by the
    /// service once the request carried the flag; a profile without it is fetched for but never
    /// scored, so the promise holds for anyone who has not said yes.
    /// </summary>
    public DateTimeOffset? AiScoringConsentAcceptedAt { get; private set; }

    public bool HasAiScoringConsent => AiScoringConsentAcceptedAt is not null;

    /// <summary>The Monday "N postings are ready" e-mail. On by default — it is the feature's
    /// delivery channel, not marketing — and a checkbox on the criteria form turns it off.</summary>
    public bool EmailDigestEnabled { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<UserJobSourceProfileQuery> _queries = [];

    public IReadOnlyCollection<UserJobSourceProfileQuery> Queries => _queries;

    private UserJobSourceProfile()
    {
    }

    public static UserJobSourceProfile Create(Guid userId, string location, bool remoteOnly, bool enabled, int minScore,
        bool emailDigestEnabled, DateTimeOffset now)
    {
        return new UserJobSourceProfile
        {
            UserId = userId,
            Location = location,
            RemoteOnly = remoteOnly,
            Enabled = enabled,
            MinScore = Math.Clamp(minScore, MinScoreFloor, MinScoreCeiling),
            EmailDigestEnabled = emailDigestEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string location, bool remoteOnly, bool enabled, int minScore, bool emailDigestEnabled, DateTimeOffset now)
    {
        Location = location;
        RemoteOnly = remoteOnly;
        Enabled = enabled;
        MinScore = Math.Clamp(minScore, MinScoreFloor, MinScoreCeiling);
        EmailDigestEnabled = emailDigestEnabled;
        UpdatedAt = now;
    }

    /// <summary>Records the consent once; re-saving the profile keeps the original moment, because
    /// that is when it was actually given.</summary>
    public void AcceptAiScoring(DateTimeOffset now) => AiScoringConsentAcceptedAt ??= now;

    /// <summary>The user's titles in the order they gave them.</summary>
    public IEnumerable<UserJobSourceProfileQuery> OrderedQueries => _queries.OrderBy(q => q.Ordinal);

    /// <summary>Makes the title→query links match <paramref name="titles"/>, in that order; the
    /// caller has already resolved each title to its shared query. A link that survives keeps
    /// its row (the key is the query, so re-saving the same title is not a delete-and-insert).</summary>
    public void SetQueries(IEnumerable<(string Title, Guid QueryId)> titles)
    {
        var wanted = titles.Take(MaxTitles).ToList();
        _queries.RemoveAll(q => wanted.All(w => w.QueryId != q.QueryId));
        foreach (var ((title, queryId), ordinal) in wanted.Select((w, i) => (w, i)))
        {
            var existing = _queries.FirstOrDefault(q => q.QueryId == queryId);
            if (existing is null)
            {
                _queries.Add(UserJobSourceProfileQuery.Create(Id, queryId, title, ordinal));
            }
            else
            {
                existing.Update(title, ordinal);
            }
        }
    }
}

public sealed class UserJobSourceProfileQuery
{
    public Guid ProfileId { get; private set; }

    public Guid QueryId { get; private set; }

    /// <summary>The title as the user typed it; the query holds the normalised form.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Position in the user's list, 0 first.</summary>
    public int Ordinal { get; private set; }

    private UserJobSourceProfileQuery()
    {
    }

    internal static UserJobSourceProfileQuery Create(Guid profileId, Guid queryId, string title, int ordinal) =>
        new() { ProfileId = profileId, QueryId = queryId, Title = title, Ordinal = ordinal };

    internal void Update(string title, int ordinal)
    {
        Title = title;
        Ordinal = ordinal;
    }
}
