using System.Net;
using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// Extracts standard, publicly-published preview metadata (Open Graph tags, falling back to
/// &lt;title&gt;) from a raw HTML string — the same summary data a site exposes for link
/// unfurling in Slack/Twitter/etc., not a scrape of a site's internal page structure. Pure
/// function — no I/O; the caller is responsible for fetching the (size-capped) HTML.
/// </summary>
public static partial class OpenGraphMetadataParser
{
    public static string? ExtractProperty(string html, string property)
    {
        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            var propertyMatch = PropertyAttributeRegex().Match(tag.Value);
            if (!propertyMatch.Success || !propertyMatch.Groups[1].Value.Equals(property, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var contentMatch = ContentAttributeRegex().Match(tag.Value);
            if (contentMatch.Success)
            {
                return WebUtility.HtmlDecode(contentMatch.Groups[1].Value).Trim();
            }
        }

        return null;
    }

    public static string? ExtractTitleTag(string html)
    {
        var match = TitleTagRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    /// <summary>
    /// Extracts &lt;link rel="{rel}" href="..."&gt; — e.g. rel="canonical", which LinkedIn's job
    /// pages always carry pointing at the slug-based URL (title-at-company-id) even when the
    /// request itself got served at 200 with no redirect. Cheaper and more reliable than relying
    /// on redirect behavior, which varies with User-Agent.
    /// </summary>
    public static string? ExtractLinkHref(string html, string rel)
    {
        foreach (Match tag in LinkTagRegex().Matches(html))
        {
            var relMatch = RelAttributeRegex().Match(tag.Value);
            if (!relMatch.Success || !relMatch.Groups[1].Value.Equals(rel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hrefMatch = HrefAttributeRegex().Match(tag.Value);
            if (hrefMatch.Success)
            {
                return WebUtility.HtmlDecode(hrefMatch.Groups[1].Value).Trim();
            }
        }

        return null;
    }

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"(?:property|name)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex PropertyAttributeRegex();

    [GeneratedRegex(@"content\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ContentAttributeRegex();

    [GeneratedRegex(@"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTagRegex();

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"rel\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex RelAttributeRegex();

    [GeneratedRegex(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttributeRegex();
}
