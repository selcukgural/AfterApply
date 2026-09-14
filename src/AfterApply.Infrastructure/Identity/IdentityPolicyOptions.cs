namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Account-security policy, bound from the <c>Identity</c> configuration section. The defaults here
/// are the values that used to be hardcoded in <c>DependencyInjection.AddIdentityAndJwt</c>: they
/// still apply when the section is absent, so a missing/partial section can never silently fall
/// back to ASP.NET Identity's weaker built-in defaults (6-character passwords). Overriding any of
/// them is a configuration change (appsettings or an <c>Identity__Password__RequiredLength</c>-style
/// environment variable on Cloud Run), not a code change.
///
/// The password rules are also what <c>GET /api/config</c> publishes, so the web app can show the
/// same requirements up front instead of letting the user discover them one rejected submit at a
/// time.
/// </summary>
public sealed class IdentityPolicyOptions
{
    public const string SectionName = "Identity";

    public PasswordPolicyOptions Password { get; init; } = new();

    public LockoutPolicyOptions Lockout { get; init; } = new();

    /// <summary>How long a password-reset link stays valid. Identity's default is one day, which is
    /// too long for a link that lands in an inbox. Keep the seeded PasswordReset email template's
    /// "expires in N minutes" sentence in step with this (it's a DB row, editable without a deploy).</summary>
    public int PasswordResetTokenMinutes { get; init; } = 30;

    /// <summary>
    /// Length over composition, per NIST SP 800-63B §5.1.1.2 (and ASVS 4.0 V2.1, which forbids
    /// composition rules outright): a 12-character minimum is the whole policy. The four character-
    /// class rules were on until 2026-09-14; together with the length they made the sign-up form
    /// the heaviest step of a free product whose landing page converted 0% of visitors, and they buy
    /// little — a password that is long and not the same character repeated is what resists
    /// guessing, not one that happens to contain a "!". <c>RequiredUniqueChars</c> stays at 4 so the
    /// length rule cannot be met with "aaaaaaaaaaaa". Only new/changed passwords are evaluated;
    /// sign-in never re-checks the policy, so existing accounts are unaffected either way.
    /// </summary>
    public sealed class PasswordPolicyOptions
    {
        public int RequiredLength { get; init; } = 12;

        public int RequiredUniqueChars { get; init; } = 4;

        public bool RequireDigit { get; init; } = false;

        public bool RequireLowercase { get; init; } = false;

        public bool RequireUppercase { get; init; } = false;

        public bool RequireNonAlphanumeric { get; init; } = false;
    }

    public sealed class LockoutPolicyOptions
    {
        /// <summary>Per-account brute-force bound — the control the IP-based auth rate limiter can't
        /// provide on its own (an attacker spread across many IPs still hits this).</summary>
        public int MaxFailedAccessAttempts { get; init; } = 5;

        public int LockoutMinutes { get; init; } = 15;
    }
}
