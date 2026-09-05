namespace AfterApply.Api;

/// <summary>
/// Rate-limit sizing, bound from the <c>RateLimiting</c> section. Defaults are the values that were
/// hardcoded in <see cref="RateLimiting"/> until 2026-09-05, so an absent section changes nothing;
/// tightening or loosening a bucket is now a config change rather than a redeploy. See the comments
/// on each policy in <see cref="RateLimiting.AddApiRateLimiting"/> for why each bucket is sized the
/// way it is.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Exists for the integration suite (see Program.cs); nothing deployed sets it.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Backstop over every endpoint, per user (or per IP when anonymous).</summary>
    public FixedWindowPolicy Global { get; init; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Per IP: login/register/refresh/forgot/reset run before the caller is authenticated.</summary>
    public FixedWindowPolicy Auth { get; init; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public FixedWindowPolicy Upload { get; init; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    public FixedWindowPolicy ExtensionSignal { get; init; } = new() { PermitLimit = 60, WindowSeconds = 300 };

    public FixedWindowPolicy LinkPreview { get; init; } = new() { PermitLimit = 20, WindowSeconds = 300 };

    public sealed class FixedWindowPolicy
    {
        public int PermitLimit { get; init; }

        public int WindowSeconds { get; init; }

        public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
    }
}
