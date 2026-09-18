using Microsoft.AspNetCore.Identity;

namespace AfterApply.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ConsentAcceptedAt { get; set; }

    /// <summary>
    /// Whether this account may read the internal admin surfaces (today: product metrics).
    ///
    /// A column rather than a config allowlist, and that is a deployment decision. Config —
    /// appsettings, an env var, a Secret Manager secret — is read when the process starts, so
    /// changing who is an admin means waiting for a new Cloud Run instance or forcing one with a
    /// redeploy. Read from here it takes effect on the next request, because the check already
    /// queries this table anyway.
    ///
    /// Granted and revoked by hand with SQL against the database, the same way FeedbackEntry.Status
    /// is: there is no admin UI, and a mutator nothing calls would be dead code. The consequence
    /// worth knowing is that there is no record of who granted it or when — acceptable while this
    /// means "can read aggregate counts", and the thing to revisit if it ever means more.
    ///
    /// Note this is deliberately NOT keyed on the email address: an allowlist of addresses hands
    /// access to whoever registers an address someone else abandoned.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// How many company reviews this account may hold in total, when an admin has set it for
    /// this account specifically; null means the global <c>CompanyReviews:MaxReviewsPerUser</c>
    /// applies. A column for the same reason <see cref="IsAdmin"/> is one: it changes on the next
    /// request, not on the next deploy — which is what "set a spammer to 0" has to mean. Set through
    /// the admin moderation endpoint (the one admin write this table has).
    /// </summary>
    public int? ReviewQuotaOverride { get; set; }

    /// <summary>ISO 639-1 code ("tr"/"en") applied to this user's session right after login,
    /// regardless of which device/browser they sign in from. Kept in sync with the frontend's
    /// current UI locale whenever the user switches languages while authenticated.</summary>
    public string PreferredLanguage { get; set; } = "tr";

    /// <summary>"light"/"dark" applied to this user's session right after login, regardless of
    /// which device/browser they sign in from. Kept in sync with the frontend's current theme
    /// whenever the user switches themes while authenticated.</summary>
    public string PreferredTheme { get; set; } = "light";

    /// <summary>
    /// When the user last answered the dashboard's "mark your stale applications as ghosted?"
    /// question with "not now". The question stays away until an application created after this
    /// moment turns out to be stale as well — a later import — so a "no" is honoured without
    /// becoming permanent. Null: never dismissed.
    /// </summary>
    public DateTimeOffset? StaleSuggestionDismissedAt { get; set; }

    /// <summary>
    /// When the user closed the dashboard's "weekly postings are here" announcement. One-shot:
    /// a closed announcement never returns for this account, on any device. Null: not closed
    /// (or never shown — the card only appears to accounts that are not Pro while the plan is on
    /// sale). Lives here rather than in a preferences table because it is the second such flag
    /// (StaleSuggestionDismissedAt is the first) and a table for two nullable timestamps is not
    /// yet worth its migration.
    /// </summary>
    public DateTimeOffset? WeeklyJobsAnnouncementDismissedAt { get; set; }

    /// <summary>
    /// The break the user asked for (DEVELOPMENT_PLAN.md, T-series, T5): from when, and until when,
    /// the dashboard keeps reminders and the stale-applications question out of sight. The scan
    /// keeps running underneath — the rows are what lets the return say "N went quiet while you were
    /// away" — only the showing stops. Both null: no break. Until in the past: the break is over
    /// and the return question is still waiting to be answered; answering clears both. Third and
    /// fourth nullable timestamps on the user row, same reasoning as the two above.
    /// </summary>
    public DateTimeOffset? RemindersPausedFrom { get; set; }

    public DateTimeOffset? RemindersPausedUntil { get; set; }
}
