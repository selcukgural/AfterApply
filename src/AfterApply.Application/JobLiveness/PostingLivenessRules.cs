using System.Globalization;
using System.Text.RegularExpressions;
using AfterApply.Application.Common;
using AfterApply.Domain.Jobs;

namespace AfterApply.Application.JobLiveness;

/// <summary>
/// How LinkedIn and kariyer.net say a posting has closed, read from a plain logged-out fetch —
/// measured against live and closed postings on 2026-09-27 (see DECISIONS.md). Pure: the client
/// hands in what came back, these rules say what it means.
/// </summary>
/// <remarks>
/// LinkedIn: a closed posting that is still shown carries <c>&lt;figure class="closed-job …"&gt;</c>
/// ("No longer accepting applications", "Artık başvuru kabul etmiyor" in Turkish — the class is
/// matched, not the words, so the page's language does not matter). An expired one answers with a
/// redirect to a regional search page whose query carries <c>trk=expired_jd_redirect</c>.
/// <para>
/// kariyer.net: the page's state carries <c>closingDateNumeric:"dd.MM.yyyy"</c> on open and closed
/// postings alike, so a date in the past is the close and a date ahead is the announced close. Its
/// <c>passiveReason</c> field is NOT a signal — it holds the same sentence on open postings. A
/// removed posting redirects to the general <c>/is-ilanlari</c> listing, which says nothing about
/// why, so that is only a "gone".
/// </para>
/// </remarks>
public static partial class PostingLivenessRules
{
    private static readonly TimeZoneInfo SiteTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static bool IsLinkedInExpiredRedirect(Uri location) =>
        HostRules.IsHttpsHost(location, "linkedin.com")
        && location.Query.TrimStart('?').Split('&').Any(pair => pair.Equals("trk=expired_jd_redirect", StringComparison.OrdinalIgnoreCase));

    public static PostingLiveness ReadLinkedInPage(string html) =>
        LinkedInClosedMarker().IsMatch(html) ? PostingLiveness.Closed() : PostingLiveness.Open();

    public static bool IsKariyerNetRemovedRedirect(Uri location) =>
        HostRules.IsHttpsHost(location, "kariyer.net")
        && location.AbsolutePath.TrimEnd('/').Equals("/is-ilanlari", StringComparison.OrdinalIgnoreCase);

    /// <param name="now">The check time; "past" is judged on Istanbul's calendar, the site's own.</param>
    public static PostingLiveness ReadKariyerNetPage(string html, DateTimeOffset now)
    {
        var match = KariyerNetClosingDate().Match(html);
        if (!match.Success
            || !DateOnly.TryParseExact(match.Groups[1].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closingDay))
        {
            // The page is there and says nothing either way: treat it as up.
            return PostingLiveness.Open();
        }

        // The closing date is the last day applications are taken, so it closes at the start of
        // the next Istanbul day.
        var closesAt = StartOfDay(closingDay.AddDays(1));
        return closesAt <= now ? PostingLiveness.Closed(closesAt) : PostingLiveness.Open(closesAt);
    }

    private static DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, SiteTimeZone.GetUtcOffset(local));
    }

    [GeneratedRegex("""class="closed-job[\s"]""", RegexOptions.CultureInvariant)]
    private static partial Regex LinkedInClosedMarker();

    [GeneratedRegex(@"closingDateNumeric:""(\d{2}\.\d{2}\.\d{4})""", RegexOptions.CultureInvariant)]
    private static partial Regex KariyerNetClosingDate();
}
