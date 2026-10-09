namespace AfterApply.Domain.SiteTraffic;

/// <summary>
/// The closed set of things the public site is allowed to report. Closed on purpose: the counter
/// table's row count is the product of this set and the path allowlist, so an open-ended event name
/// would let any caller grow the table without bound — and would make the admin view unreadable.
///
/// Stored as a string (see SiteTrafficDailyCounterConfiguration) so adding a member is a code
/// change rather than a migration.
/// </summary>
public enum SiteTrafficEvent
{
    /// <summary>A public page was opened. The denominator of everything else.</summary>
    PageView,

    /// <summary>The landing page's primary call to action was clicked.</summary>
    CtaGetStarted,

    /// <summary>The registration form was submitted.</summary>
    RegisterStarted,

    /// <summary>A CV scan finished and a score was shown. The step the V6 funnel turns on: a page
    /// view says someone arrived, this says they got the thing the page promised — and the gap
    /// between the two is where an upload that never completes would hide.</summary>
    CvScanCompleted,

    /// <summary>Registration came back successful. Together with RegisterStarted this separates
    /// "nobody tries" from "people try and the form rejects them" — two very different problems
    /// that a single conversion number hides.</summary>
    RegisterCompleted,

    /// <summary>A share button was used — a CV score, a benchmark result or a company page handed
    /// to someone else. The one number that says whether the product produces anything a person
    /// wants to pass on (growth audit 2026-09-14, finding 03/14).</summary>
    ShareClicked,

    /// <summary>The header's "Ücretsiz Başla" button was clicked (2026-10-02). Kept apart from
    /// CtaGetStarted, which counts the buttons inside the page: the header is the one call to
    /// action on every public page, and it went uncounted until then, so the page's own number
    /// read lower than the traffic into /register said it should.</summary>
    CtaHeaderGetStarted,

    /// <summary>The sign-up form was sent with a password the policy refused, counted once per
    /// visit to the form. Answers "is the password rule what stops people", which RegisterStarted
    /// cannot: that one is only counted once the form is valid.</summary>
    RegisterPasswordRejected,

    /// <summary>A LinkedIn, Google or GitHub sign-in turned out to be a new account and the
    /// "complete your sign-up" form was shown. Most accounts arrive this way, and none of them
    /// were in the funnel until 2026-10-02.</summary>
    RegisterSocialStarted,

    /// <summary>That form was sent and the account was created — the social counterpart of
    /// RegisterCompleted.</summary>
    RegisterSocialCompleted,

    /// <summary>A LinkedIn, Google or GitHub button on the landing page's sign-up card was clicked
    /// (2026-10-02). The card put the providers on the first screen; this says how many of the
    /// social sign-ups started there rather than on /register.</summary>
    CtaHeroSocialSignIn,

    /// <summary>A free tool other than the CV scan gave the visitor its result (2026-10-09): the
    /// benchmark answer came back, the offer comparison got its first figure that is not the
    /// example, a "no reply" report on a company page was sent. The path says which tool. The CV
    /// scan keeps CvScanCompleted so its history stays one series.</summary>
    ToolResultShown,

    /// <summary>A sign-up button inside a free tool was clicked — the CV scan or benchmark result,
    /// the response-rate table, the "no reply" report's thank-you (2026-10-09). The path says
    /// which tool. Answers "does anyone who got a result reach for an account", which the tool's
    /// own result count and the site-wide sign-up count cannot answer between them.</summary>
    CtaToolSignUp
}
