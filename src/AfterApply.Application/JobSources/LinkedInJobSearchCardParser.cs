using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AfterApply.Application.Imports;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

/// <summary>
/// Reads the cards out of one page of LinkedIn's public job-search fragment
/// (<c>/jobs-guest/jobs/api/seeMoreJobPostings/search</c>, verified 2026-09-12): each card is a
/// <c>&lt;li&gt;</c> carrying <c>data-entity-urn="urn:li:jobPosting:{id}"</c>, an <c>h3</c> title,
/// an <c>h4</c> company link, a location span, a <c>&lt;time datetime&gt;</c> and the posting's
/// slug URL. Pure function over untrusted HTML — regex + decode, no DOM, nothing executed. A card
/// missing its id or title is skipped rather than guessed at.
/// </summary>
public static partial class LinkedInJobSearchCardParser
{
    public static IReadOnlyList<JobSourceCard> Parse(string html)
    {
        var cards = new List<JobSourceCard>();
        foreach (Match item in ListItemRegex().Matches(html))
        {
            var card = ParseCard(item.Groups[1].Value);
            if (card is not null)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private static JobSourceCard? ParseCard(string li)
    {
        var urn = UrnRegex().Match(li);
        var title = Clean(TitleRegex().Match(li).Groups[1].Value);
        if (!urn.Success || title.Length == 0)
        {
            return null;
        }

        var externalId = urn.Groups[1].Value;
        var subtitle = SubtitleRegex().Match(li);
        var company = subtitle.Success ? Clean(subtitle.Groups[2].Value) : string.Empty;
        var companyUrl = subtitle.Success ? StripQuery(WebUtility.HtmlDecode(subtitle.Groups[1].Value)) : null;
        var location = LocationRegex().Match(li) is { Success: true } loc ? NullIfEmpty(Clean(loc.Groups[1].Value)) : null;

        DateOnly? postedAt = DateRegex().Match(li) is { Success: true } date
                             && DateOnly.TryParseExact(date.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                 DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

        // The slug link carries tracking parameters; the bare /jobs/view/{id} form is what we
        // store and show, and it is also what LinkedInJobIdExtractor reads back from an
        // application's URL when the two are compared.
        var url = "https://www.linkedin.com/jobs/view/" + externalId;

        return new JobSourceCard(externalId, title, company, NullIfEmpty(companyUrl), location, postedAt, url);
    }

    private static string Clean(string fragment)
    {
        var stripped = TagRegex().Replace(fragment, " ");
        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(stripped), " ").Trim();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? StripQuery(string? url)
    {
        if (url is null)
        {
            return null;
        }

        var q = url.IndexOf('?');
        return q < 0 ? url : url[..q];
    }

    [GeneratedRegex(@"<li\b[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"data-entity-urn\s*=\s*""urn:li:jobPosting:(\d{1,20})""", RegexOptions.IgnoreCase)]
    private static partial Regex UrnRegex();

    [GeneratedRegex(@"<h3[^>]*base-search-card__title[^>]*>(.*?)</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<h4[^>]*base-search-card__subtitle[^>]*>\s*<a[^>]*href\s*=\s*""([^""]*)""[^>]*>(.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SubtitleRegex();

    [GeneratedRegex(@"<span[^>]*job-search-card__location[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"<time[^>]*datetime\s*=\s*""(\d{4}-\d{2}-\d{2})""", RegexOptions.IgnoreCase)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
