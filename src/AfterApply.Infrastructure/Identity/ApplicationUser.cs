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
}
