using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSources;

/// <summary>
/// Per-user overrides of the sweep's product limits, written only by an admin. Null means "the
/// global default"; the row goes when nothing is overridden. Separate from the profile so that a
/// user editing their criteria can never touch their own ceiling.
/// </summary>
public sealed class UserJobSourceSettings : Entity
{
    public Guid UserId { get; private set; }

    public int? WeeklyPostingLimit { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private UserJobSourceSettings()
    {
    }

    public static UserJobSourceSettings Create(Guid userId, int? weeklyPostingLimit, DateTimeOffset now) =>
        new() { UserId = userId, WeeklyPostingLimit = weeklyPostingLimit, UpdatedAt = now };

    public void Update(int? weeklyPostingLimit, DateTimeOffset now)
    {
        WeeklyPostingLimit = weeklyPostingLimit;
        UpdatedAt = now;
    }

    public bool IsEmpty => WeeklyPostingLimit is null;
}
