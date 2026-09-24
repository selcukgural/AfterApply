namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Email verification at sign-up and the limits on account emails (2026-09-24), bound from the
/// <c>EmailVerification</c> section. The ceilings exist because Resend's free plan sends 100 emails
/// a day for the whole product: without them one script could spend the day's quota in minutes and
/// leave every real sign-up and password reset silently unsent.
/// </summary>
public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    /// <summary>How long an emailed code works.</summary>
    public int CodeLifetimeMinutes { get; init; } = 15;

    /// <summary>How long the browser's ticket works; a resend inside it keeps the same ticket.</summary>
    public int TicketLifetimeMinutes { get; init; } = 60;

    /// <summary>Wrong guesses a single code survives; a new code resets the count.</summary>
    public int MaxAttemptsPerCode { get; init; } = 5;

    /// <summary>A sign-up that never verified is deleted after this many days, which frees its
    /// address for whoever actually owns it.</summary>
    public int UnverifiedAccountRetentionDays { get; init; } = 7;

    public string CleanupCronExpression { get; init; } = "30 3 * * *";

    public AuthEmailLimit Verification { get; init; } = new() { CooldownSeconds = 60, PerAccountDaily = 5, GlobalDaily = 60 };

    public AuthEmailLimit PasswordReset { get; init; } = new() { CooldownSeconds = 300, PerAccountDaily = 3, GlobalDaily = 30 };

    public TimeSpan CodeLifetime => TimeSpan.FromMinutes(CodeLifetimeMinutes);

    public TimeSpan TicketLifetime => TimeSpan.FromMinutes(TicketLifetimeMinutes);

    public AuthEmailLimit LimitFor(AuthEmailKind kind) => kind switch
    {
        AuthEmailKind.EmailVerification => Verification,
        AuthEmailKind.PasswordReset => PasswordReset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

public sealed class AuthEmailLimit
{
    /// <summary>Minimum gap between two emails of this kind to one account.</summary>
    public int CooldownSeconds { get; init; }

    /// <summary>Emails of this kind one account can receive in a UTC day.</summary>
    public int PerAccountDaily { get; init; }

    /// <summary>Emails of this kind the whole product sends in a UTC day — kept below the provider
    /// quota together with the other kinds.</summary>
    public int GlobalDaily { get; init; }
}
