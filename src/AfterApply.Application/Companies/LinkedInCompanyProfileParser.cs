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
        if (target is null)
        {
            return null;
        }

        // Copied from a third-party page into a column we render as a link: anything but a plain
        // http(s) URL is dropped here, like the kariyer.net parser does.
        var url = WebUtility.HtmlDecode(target).Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;
    }

    /// <summary>
    /// The name the page gives the company — the schema.org Organization block's <c>name</c>, or
    /// failing that the <c>og:title</c> without its " | LinkedIn" tail. Compared against the
    /// company's own name before anything on the page is trusted (see <see cref="CompanyPageIdentity"/>).
    /// </summary>
    public static string? ExtractCompanyName(string html)
    {
        var jsonLd = OrganizationNameRegex().Match(html);
        if (jsonLd.Success)
        {
            var name = Regex.Unescape(jsonLd.Groups[1].Value).Trim();
            if (name.Length > 0)
            {
                return name;
            }
        }

        var ogTitle = OgTitleRegex().Match(html);
        if (!ogTitle.Success)
        {
            return null;
        }

        var title = WebUtility.HtmlDecode(ogTitle.Groups[1].Value).Trim();
        const string tail = "| LinkedIn";
        if (title.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^tail.Length].TrimEnd();
        }

        return title.Length == 0 ? null : title;
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

    [GeneratedRegex(@"""@type""\s*:\s*""Organization""\s*,\s*""name""\s*:\s*""((?:[^""\\]|\\.)*)""")]
    private static partial Regex OrganizationNameRegex();

    [GeneratedRegex(@"<meta\s+property=""og:title""\s+content=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex OgTitleRegex();
}
