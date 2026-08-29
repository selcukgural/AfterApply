using System.Net;
using System.Text.RegularExpressions;

namespace AfterApply.Application.Companies;

/// <summary>
/// Extracts company-profile fields (Industry, Website, Country) from a LinkedIn company page's
/// server-rendered HTML — the public "About" overview LinkedIn shows to logged-out visitors
/// (confirmed empirically: a plain HTTP GET with an honest bot User-Agent returns it fully
/// rendered, no login wall — same technique already validated for job postings in
/// JobLinkPreviewService). Pure function — no I/O; the caller fetches the (size-capped) HTML.
/// </summary>
public static partial class LinkedInCompanyProfileParser
{
    public static string? ExtractIndustry(string html) => ExtractOverviewField(html, "Industry");

    /// <summary>
    /// The "About" overview's Website row links through LinkedIn's own click-tracking redirect
    /// (<c>linkedin.com/redir/redirect?url={encoded target}&amp;...</c>) rather than the target
    /// site directly — the real URL is that redirect's own "url" query parameter.
    /// </summary>
    public static string? ExtractWebsite(string html)
    {
        var fieldHtml = ExtractOverviewFieldHtml(html, "Website");
        if (fieldHtml is null)
        {
            return null;
        }

        var hrefMatch = HrefAttributeRegex().Match(fieldHtml);
        if (!hrefMatch.Success)
        {
            return null;
        }

        var href = WebUtility.HtmlDecode(hrefMatch.Groups[1].Value);
        var target = ExtractQueryParam(href, "url");
        return target is null ? null : WebUtility.HtmlDecode(target);
    }

    /// <summary>
    /// LinkedIn embeds a schema.org Organization JSON-LD block with a structured
    /// <c>address.addressCountry</c> ISO-3166 alpha-2 code — cheaper and more reliable than trying
    /// to geocode the free-text "Headquarters" row (which is a city/region, not a country code).
    /// </summary>
    public static string? ExtractCountryCode(string html)
    {
        var match = AddressCountryRegex().Match(html);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? ExtractOverviewField(string html, string label)
    {
        var fieldHtml = ExtractOverviewFieldHtml(html, label);
        if (fieldHtml is null)
        {
            return null;
        }

        var stripped = TagRegex().Replace(fieldHtml, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped).Trim();
        return decoded.Length == 0 ? null : decoded;
    }

    private static string? ExtractOverviewFieldHtml(string html, string label)
    {
        var match = Regex.Match(
            html,
            $@"<dt[^>]*>\s*{Regex.Escape(label)}\s*</dt>\s*<dd[^>]*>(.*?)</dd>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractQueryParam(string url, string paramName)
    {
        var queryStart = url.IndexOf('?');
        if (queryStart < 0)
        {
            return null;
        }

        foreach (var pair in url[(queryStart + 1)..].Split('&'))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == paramName)
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }

    [GeneratedRegex(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttributeRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"""addressCountry""\s*:\s*""([A-Za-z]{2})""")]
    private static partial Regex AddressCountryRegex();
}
