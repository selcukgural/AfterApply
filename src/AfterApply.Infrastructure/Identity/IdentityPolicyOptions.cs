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

    public sealed class PasswordPolicyOptions
    {
        /// <summary>ASVS L2 recommendation. Only new/changed passwords are evaluated; sign-in never
        /// re-checks the policy, so raising it doesn't lock existing accounts out.</summary>
        public int RequiredLength { get; init; } = 12;

        public int RequiredUniqueChars { get; init; } = 4;

        public bool RequireDigit { get; init; } = true;

        public bool RequireLowercase { get; init; } = true;

        public bool RequireUppercase { get; init; } = true;

        public bool RequireNonAlphanumeric { get; init; } = true;
    }

    public sealed class LockoutPolicyOptions
    {
        /// <summary>Per-account brute-force bound — the control the IP-based auth rate limiter can't
        /// provide on its own (an attacker spread across many IPs still hits this).</summary>
        public int MaxFailedAccessAttempts { get; init; } = 5;

        public int LockoutMinutes { get; init; } = 15;
    }
}
