namespace AfterApply.Application.FeatureFlags.Contracts;

/// <summary>What else has to stay true for as long as a flag is on — shown next to the switch.</summary>
public enum FeatureFlagCoupling
{
    /// <summary>The privacy policy names a recipient or a transfer only this flag causes; the text
    /// and the flag move together, in both directions.</summary>
    PrivacyText,

    /// <summary>While on, the flag spends money (a paid model call) or takes it (checkout).</summary>
    Money
}

/// <summary>One flag as the admin panel shows it.</summary>
/// <param name="Title">The feature's name, in the request's language.</param>
/// <param name="Description">What the flag switches on, in plain words.</param>
/// <param name="WhenOff">What users and the system see while it is off, what is kept, and what
/// does not come back when it is switched on again.</param>
/// <param name="Notes">What moves with it (privacy text, money, other flags, configuration); null when nothing.</param>
/// <param name="Enabled">What the product does right now: the override if there is one, else the default.</param>
/// <param name="Default">The deploy default (appsettings.json + the deploy workflow's env vars).</param>
/// <param name="Override">The runtime value; null when the flag runs on its default.</param>
/// <param name="MissingPrerequisite">A code naming the configuration this deployment lacks for the
/// flag to work when on (<see cref="FeatureFlagPrerequisites"/>); switching it on is refused while set.</param>
public sealed record FeatureFlagResponse(
    FeatureFlag Flag,
    string Title,
    string Description,
    string WhenOff,
    string? Notes,
    bool Enabled,
    bool Default,
    bool? Override,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<FeatureFlagCoupling> Couplings,
    string? MissingPrerequisite);

/// <summary>First confirmation step: what the admin wants and why. Nothing changes yet.</summary>
/// <param name="Enabled">The new override; null removes it, returning the flag to its deploy default.</param>
/// <param name="Reason">Why — required, kept in the history.</param>
public sealed record PrepareFeatureFlagChangeRequest(bool? Enabled, string? Reason);

/// <summary>What the first step hands back for the second: the change spelled out, and a
/// short-lived token that only this admin can redeem for exactly this change.</summary>
/// <param name="ConfirmationPhrase">What the admin has to type to confirm — the flag's own name.</param>
/// <param name="WillBeOn">Whether the feature will be on once confirmed (the default included for a reset).</param>
/// <param name="ExpiresInSeconds">The token's lifetime, for a countdown that must not depend on the
/// admin's own clock agreeing with the server's (<paramref name="ExpiresAt"/> is the server's clock).</param>
public sealed record PrepareFeatureFlagChangeResponse(
    FeatureFlagResponse Current,
    bool? Enabled,
    bool WillBeOn,
    string ConfirmationPhrase,
    string ConfirmationToken,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);

/// <summary>Second confirmation step.</summary>
public sealed record ConfirmFeatureFlagChangeRequest(string? ConfirmationToken, string? ConfirmationPhrase);

public sealed record FeatureFlagChangeResponse(
    Guid Id,
    FeatureFlag Flag,
    bool? Enabled,
    bool WasOn,
    bool IsOn,
    string? Reason,
    DateTimeOffset ChangedAt,
    string? ChangedBy);

/// <summary>
/// Where each flag's texts live in <c>SharedStrings</c> (tr + en): <c>FEATURE_FLAG_{NAME}_TITLE</c>,
/// <c>_DESCRIPTION</c>, <c>_WHEN_OFF</c> and, when there is something to say, <c>_NOTES</c> — NAME
/// being the member name in upper snake case (CvScanNotes → CV_SCAN_NOTES).
/// </summary>
public static class FeatureFlagTexts
{
    public static string KeyPrefix(FeatureFlag flag)
    {
        var name = flag.ToString();
        var builder = new System.Text.StringBuilder("FEATURE_FLAG_");
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(name[i]));
        }

        return builder.ToString();
    }
}

/// <summary>The codes <see cref="FeatureFlagResponse.MissingPrerequisite"/> carries.</summary>
public static class FeatureFlagPrerequisites
{
    /// <summary>Blog on Google Cloud Storage with no <c>Storage:BlogMediaBucketName</c>.</summary>
    public const string BlogMediaBucket = "BLOG_MEDIA_BUCKET_MISSING";

    /// <summary>No <c>CvScan:Review:ProjectId</c>: the notes would have no Vertex project to call.</summary>
    public const string CvScanNotesProject = "CV_SCAN_NOTES_PROJECT_MISSING";

    /// <summary>No <c>Feedback:GitHub:Repository</c> / <c>Token</c>.</summary>
    public const string FeedbackGitHubTarget = "FEEDBACK_GITHUB_TARGET_MISSING";

    /// <summary>The PayTR merchant values or plan prices are not set (the start-up validator's rules).</summary>
    public const string PayTrConfiguration = "PAYTR_NOT_CONFIGURED";
}

public enum PrepareFeatureFlagOutcome
{
    Ready,

    /// <summary>The flag is already exactly that (same override, or no override and a reset asked).</summary>
    Unchanged,

    /// <summary>Switching on was asked while <see cref="FeatureFlagResponse.MissingPrerequisite"/> is set.</summary>
    MissingPrerequisite
}

public sealed record PrepareFeatureFlagChangeResult(PrepareFeatureFlagOutcome Outcome, FeatureFlagResponse Current,
    PrepareFeatureFlagChangeResponse? Ready);

public enum ConfirmFeatureFlagOutcome
{
    Changed,

    /// <summary>The token is unreadable, expired, or was issued to another admin or for another flag.</summary>
    InvalidToken,

    /// <summary>The typed phrase is not the flag's name.</summary>
    PhraseMismatch,

    /// <summary>The flag changed after the first step (another admin, another tab, or this token
    /// already used) — the admin starts again from what is there now.</summary>
    Stale,

    /// <summary>The configuration a switch-on needs went missing between the two steps.</summary>
    MissingPrerequisite
}

public sealed record ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome Outcome, FeatureFlagResponse? Flag);
