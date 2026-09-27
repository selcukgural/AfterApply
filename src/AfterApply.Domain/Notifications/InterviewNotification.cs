using AfterApply.Domain.Common;

namespace AfterApply.Domain.Notifications;

/// <summary>Which of the two interview moments a bell row is about.</summary>
public enum InterviewNotificationKind
{
    /// <summary>The interview is within the next day and a half: "tomorrow at 14:00".</summary>
    Upcoming,

    /// <summary>The interview is over and "how did it go?" is waiting on the reminders card.</summary>
    Held
}

/// <summary>
/// A bell row about one interview of the user's own. Written by the nightly reminder scan — one per
/// interview per kind, keyed by the interview's instant so a moved interview gets fresh rows — and
/// only while the user's "interview reminders" preference is on. The feed hides a row whose
/// interview is no longer the application's current one, so a moved or answered interview does not
/// linger as news.
/// </summary>
public sealed class InterviewNotification : Entity
{
    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public InterviewNotificationKind Kind { get; private set; }

    public DateTimeOffset InterviewAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset? DismissedAt { get; private set; }

    private InterviewNotification()
    {
    }

    public static InterviewNotification Create(Guid userId, Guid applicationId, InterviewNotificationKind kind,
        DateTimeOffset interviewAt, DateTimeOffset now) => new()
    {
        UserId = userId,
        ApplicationId = applicationId,
        Kind = kind,
        InterviewAt = interviewAt,
        CreatedAt = now
    };

    public void Dismiss(DateTimeOffset now) => DismissedAt ??= now;
}
