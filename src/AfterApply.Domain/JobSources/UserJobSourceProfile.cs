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

    public Guid UserId { get; private set; }

    public string Location { get; private set; } = string.Empty;

    public bool RemoteOnly { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<UserJobSourceProfileQuery> _queries = [];

    public IReadOnlyCollection<UserJobSourceProfileQuery> Queries => _queries;

    private UserJobSourceProfile()
    {
    }

    public static UserJobSourceProfile Create(Guid userId, string location, bool remoteOnly, bool enabled, DateTimeOffset now)
    {
        return new UserJobSourceProfile
        {
            UserId = userId,
            Location = location,
            RemoteOnly = remoteOnly,
            Enabled = enabled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string location, bool remoteOnly, bool enabled, DateTimeOffset now)
    {
        Location = location;
        RemoteOnly = remoteOnly;
        Enabled = enabled;
        UpdatedAt = now;
    }

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
