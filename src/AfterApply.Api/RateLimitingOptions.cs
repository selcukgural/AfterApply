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

    /// <summary>Per user. An hour-long window because this is a human writing prose, not a
    /// screen making requests — five sends an hour is generous for that and still caps what a
    /// scripted account can push into the feedback table (and, when the mirror is on, into a
    /// GitHub repository).</summary>
    public FixedWindowPolicy Feedback { get; init; } = new() { PermitLimit = 5, WindowSeconds = 3600 };

    /// <summary>Per IP, and anonymous by nature — the public site's visit counter. Sized for a
    /// person browsing, not for a beacon per interaction: a real session opens a handful of pages,
    /// so 120 in five minutes is far above normal use while still bounding what a script can push
    /// into the counter table. Nothing here is user data, so an over-count is a cosmetic problem
    /// rather than a leak; the limit exists to keep the table honest, not to protect anything.</summary>
    public FixedWindowPolicy SiteTraffic { get; init; } = new() { PermitLimit = 120, WindowSeconds = 300 };

    /// <summary>Per IP, anonymous — the public benchmark form. An hour-long window because a
    /// person answers this once and a correction is the only honest second attempt; five leaves
    /// room for that while bounding what one source can push into a median. It is the main defence
    /// available: the usual one, a CAPTCHA, is a third-party script the CSP forbids and the Cookie
    /// Policy denies the site carries.</summary>
    public FixedWindowPolicy Benchmark { get; init; } = new() { PermitLimit = 5, WindowSeconds = 3600 };

    /// <summary>Per IP, anonymous — the CV scan. The tightest anonymous bucket in the app, and
    /// deliberately: every call reads a file with a parser, which is the most expensive thing an
    /// unauthenticated stranger can ask this API to do. Five in two hours covers the honest
    /// sequence the page is built around — scan, fix the CV, scan again — and leaves room for a
    /// second file, while putting a script that wants to fingerprint the checks on a very short
    /// leash. Same defence as the benchmark form's, for the same reason: a CAPTCHA is a
    /// third-party script the CSP forbids.</summary>
    public FixedWindowPolicy CvScan { get; init; } = new() { PermitLimit = 5, WindowSeconds = 7200 };

    /// <summary>Per IP, anonymous — starting an extension pairing. Tight, because one person
    /// connecting one browser needs one of these and a retry or two; what it bounds is a script
    /// filling the table with codes waiting for someone to confirm one by mistake.</summary>
    public FixedWindowPolicy ExtensionPairingStart { get; init; } = new() { PermitLimit = 10, WindowSeconds = 300 };

    /// <summary>Per IP, anonymous — the poll behind the same flow, and sized for a machine rather
    /// than a person: an extension asks every few seconds for as long as the pairing is open, so a
    /// single honest pairing spends around two hundred of these. It is not a credential check
    /// (the device secret is 256 bits of randomness), it is a ceiling on the traffic one source can
    /// aim at the endpoint.</summary>
    public FixedWindowPolicy ExtensionPairingPoll { get; init; } = new() { PermitLimit = 600, WindowSeconds = 300 };

    public sealed class FixedWindowPolicy
    {
        public int PermitLimit { get; init; }

        public int WindowSeconds { get; init; }

        public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
    }
}
