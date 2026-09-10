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
    RegisterCompleted
}
