namespace AfterApply.Application.ClientConfig;

/// <summary>
/// Server-side limits a client needs in order to guide the user *before* a request is rejected —
/// returned by the anonymous <c>GET /api/config</c>. Every value here is also enforced server-side;
/// this only lets the web app say the same thing earlier and in one place. Nothing in it is secret.
/// </summary>
public sealed record ClientConfigResponse(
    PasswordPolicyResponse PasswordPolicy,
    PersonalAccessTokenLimitsResponse PersonalAccessTokens,
    GoogleAuthConfigResponse GoogleAuth,
    LinkedInAuthConfigResponse LinkedInAuth,
    GitHubAuthConfigResponse GitHubAuth,
    CvScanConfigResponse CvScan,
    CompanyReviewsConfigResponse? CompanyReviews = null);

/// <summary>Mirrors ASP.NET Identity's <c>PasswordOptions</c>, which is what the server actually
/// validates against — the response is built from that object, not from a copy of the config.</summary>
public sealed record PasswordPolicyResponse(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric);

public sealed record PersonalAccessTokenLimitsResponse(
    int MaxActiveTokens,
    int LifetimeDays);

/// <summary>Whether "Sign in with Google" is available and, if so, the public OAuth client id the
/// browser needs to start the redirect to accounts.google.com. <paramref name="ClientId"/> is null
/// whenever <paramref name="Enabled"/> is false. A client id is public by design (it is visible in
/// the redirect URL); the client secret never leaves the server.</summary>
public sealed record GoogleAuthConfigResponse(bool Enabled, string? ClientId);

/// <summary>Whether "Sign in with LinkedIn" is available and, if so, the public OAuth client id the
/// browser needs to start the redirect to linkedin.com. Same shape and same rules as
/// <see cref="GoogleAuthConfigResponse"/>.</summary>
public sealed record LinkedInAuthConfigResponse(bool Enabled, string? ClientId);

/// <summary>Whether "Sign in with GitHub" is available and, if so, the public OAuth client id the
/// browser needs to start the redirect to github.com. Same shape and same rules as
/// <see cref="GoogleAuthConfigResponse"/>.</summary>
public sealed record GitHubAuthConfigResponse(bool Enabled, string? ClientId);

/// <summary>
/// What the public CV scan page needs to know before it renders. Only
/// <paramref name="ContentNotesAvailable"/>: with layer B off the optional consent box is not
/// offered at all, because a checkbox for something that cannot happen is a promise the page
/// cannot keep. The scan itself is always available while the route exists — a client that asks
/// this and gets a 404 from the scan endpoint has learnt the same thing.
/// </summary>
public sealed record CvScanConfigResponse(bool ContentNotesAvailable);

/// <summary>What the public company pages and the review form need before rendering: whether the
/// feature is on at all, the quota the form should count down from, and the two numbers the
/// scoring page prints so its formula quotes the live configuration rather than a copy.</summary>
public sealed record CompanyReviewsConfigResponse(bool Enabled, int MaxReviewsPerUser, int MinimumReviewsForScore, int PriorWeight);
