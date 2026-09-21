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
    CompanyReviewsConfigResponse? CompanyReviews = null,
    CompanySalariesConfigResponse? CompanySalaries = null,
    JobSourcesConfigResponse? JobSources = null,
    PaymentsConfigResponse? Payments = null,
    CandidateExperiencesConfigResponse? CandidateExperiences = null,
    BlogConfigResponse? Blog = null,
    CompanyIntelligenceConfigResponse? CompanyIntelligence = null,
    ResponseRatesConfigResponse? ResponseRates = null);

/// <summary>Whether a company page has a "Response" tab: the CompanyIntelligence flag, off in
/// production until the legal read (DEVELOPMENT_PLAN.md K1). The tab is not rendered while off.</summary>
public sealed record CompanyIntelligenceConfigResponse(bool Enabled);

/// <summary>Whether the public sector response-rate page and its nav links exist.</summary>
public sealed record ResponseRatesConfigResponse(bool Enabled);

/// <summary>What the site chrome needs before it shows a "Blog" link: the flag, and whether
/// there is anything published to link to. A blog with no posts is not offered at all —
/// <paramref name="HasPublishedPosts"/> is false until the first publish (and cached with this
/// response for a few minutes, so the link follows the first post with a short lag).</summary>
public sealed record BlogConfigResponse(bool Enabled, bool HasPublishedPosts);

/// <summary>Whether the paid weekly job matching is switched on at all. Off means every
/// <c>/api/job-sources/*</c> route 404s and the web app shows no trace of the feature.</summary>
public sealed record JobSourcesConfigResponse(bool Enabled);

/// <summary>Whether the Pro plan can be bought right now (PayTR switched on and configured).
/// Prices are not here — this response is public and cached; they come from
/// <c>GET /api/payments/plans</c> to a signed-in user.</summary>
public sealed record PaymentsConfigResponse(bool Enabled);

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
/// <summary><paramref name="Enabled"/> is the CvScan:Enabled flag — the CV page shows its
/// "measure ATS readability" control only while the scan routes exist. Additive (2026-09-18).</summary>
public sealed record CvScanConfigResponse(bool Enabled, bool ContentNotesAvailable);

/// <summary>What the public company pages and the review form need before rendering: whether the
/// feature is on at all, the quota the form should count down from, and the two numbers the
/// scoring page prints so its formula quotes the live configuration rather than a copy.</summary>
public sealed record CompanyReviewsConfigResponse(bool Enabled, int MaxReviewsPerUser, int MinimumReviewsForScore, int PriorWeight);

/// <summary>What the contribute page and the company page's salary tab need before rendering:
/// whether the feature is on (off hides the menu links and the tab), the quota the form counts
/// down from, and the threshold under which no median is shown.</summary>
public sealed record CompanySalariesConfigResponse(bool Enabled, int MaxEntriesPerUser, int MinimumEntriesForStats);

/// <summary>What the contribute page and the company page's candidate-experience tab need before
/// rendering: whether the feature is on (off hides the menu links and the tab), the quota the
/// form counts down from, the threshold under which no aggregate is shown and the prior weight
/// the scoring page quotes.</summary>
public sealed record CandidateExperiencesConfigResponse(bool Enabled, int MaxEntriesPerUser, int MinimumEntriesForStats, int PriorWeight);
