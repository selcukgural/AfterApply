using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AfterApply.Application.Imports;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

/// <summary>
/// Reads the cards out of one server-rendered page of kariyer.net's listing (<c>/is-ilanlari/…</c>,
/// verified 2026-09-14). Each card is an <c>&lt;a data-test="ad-card-item" href="/is-ilani/{slug}-{id}"&gt;</c>
/// carrying <c>data-test</c>-tagged spans for the title, the company, the location, the work model
/// and a relative date ("3 gün", "12 saat"). The <c>data-test</c> hooks are the site's own test
/// ids, which is the most stable thing in a Vue-rendered page. Pure function over untrusted HTML —
/// regex + decode, no DOM. A card missing its id or title is skipped rather than guessed at.
/// </summary>
public static partial class KariyerNetJobSearchCardParser
{
    public static JobSourceSearchPage Parse(string html, DateOnly today, int currentPage)
    {
        var cards = new List<JobSourceCard>();
        foreach (Match item in CardRegex().Matches(html))
        {
            var card = ParseCard(item.Groups[1].Value, item.Groups[2].Value, today);
            if (card is not null)
            {
                cards.Add(card);
            }
        }

        var nextPage = (currentPage + 1).ToString(CultureInfo.InvariantCulture);
        var hasMore = cards.Count > 0 && NextPageRegex().Matches(html).Any(m => m.Groups[1].Value == nextPage);
        return new JobSourceSearchPage(cards, hasMore);
    }

    private static JobSourceCard? ParseCard(string href, string body, DateOnly today)
    {
        var path = WebUtility.HtmlDecode(href);
        var externalId = KariyerNetJobIdExtractor.Extract("https://" + KariyerNetJobSearchUrlBuilder.Host + path);
        var title = Clean(Field(body, "ad-card-title"));
        if (externalId is null || title.Length == 0)
        {
            return null;
        }

        var company = Clean(Field(body, "subtitle"));
        var location = NullIfEmpty(Clean(Field(body, "location")));
        var workModel = NullIfEmpty(Clean(Field(body, "work-model")));
        var postedAt = ParseRelativeDate(Clean(Field(body, "ad-date-item-date-other")), today);

        // The canonical URL is the card's own href — it is what the site redirects the bare id to,
        // and what KariyerNetJobIdExtractor reads back from an application's URL.
        var q = path.IndexOf('?');
        var url = "https://" + KariyerNetJobSearchUrlBuilder.Host + (q < 0 ? path : path[..q]);

        return new JobSourceCard(externalId, title, company, null, location, postedAt, url, workModel);
    }

    /// <summary>"3 gün" → three days ago, "12 saat" → today, "Bugün"/"Dün" likewise; anything
    /// else (or nothing) is unknown rather than a guess.</summary>
    public static DateOnly? ParseRelativeDate(string text, DateOnly today)
    {
        if (text.Length == 0)
        {
            return null;
        }

        if (text.Contains("bugün", StringComparison.OrdinalIgnoreCase) || text.Contains("saat", StringComparison.OrdinalIgnoreCase)
            || text.Contains("dakika", StringComparison.OrdinalIgnoreCase))
        {
            return today;
        }

        if (text.Contains("dün", StringComparison.OrdinalIgnoreCase))
        {
            return today.AddDays(-1);
        }

        var days = RelativeDaysRegex().Match(text);
        if (days.Success && int.TryParse(days.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 366)
        {
            return today.AddDays(-n);
        }

        return null;
    }

    private static string Field(string body, string testId)
    {
        var match = Regex.Match(body, @"data-test=""" + Regex.Escape(testId) + @"""[^>]*>(.*?)</(?:span|div)>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string Clean(string fragment)
    {
        var stripped = TagRegex().Replace(fragment, " ");
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(stripped), " ").Trim();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"<a\s+href=""(/is-ilani/[^""]+)""[^>]*data-test=""ad-card-item""[^>]*>(.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CardRegex();

    [GeneratedRegex(@"href=""[^""]*[?&](?:amp;)?cp=(\d+)[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex NextPageRegex();

    [GeneratedRegex(@"(\d{1,3})\s*gün", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeDaysRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
