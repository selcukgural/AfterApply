using System.Net;
using System.Text.RegularExpressions;

namespace AfterApply.Application.Companies;

/// <summary>
/// The icons a company's own home page declares, best first, for the board's logo
/// (DECISIONS.md 2026-09-28, "Şirket logosu: önce şirketin sitesi"). Pure function — no I/O; the
/// caller fetches the (size-capped) HTML and every candidate under its own fetch rules.
///
/// Best means biggest raster: an <c>apple-touch-icon</c> (commonly 180 px) before a plain
/// <c>icon</c>, and within each the declared <c>sizes</c>, largest first. SVG is never a candidate
/// (a document that can carry script); neither is <c>favicon.ico</c> (a format the store does not
/// keep, and at 16–32 px too small to be a logo anyway). Only http(s) URLs come back, resolved
/// against the page's own address.
/// </summary>
public static partial class WebsiteIconParser
{
    /// <summary>Candidates tried per company: enough for "the big one is missing, try the next".</summary>
    public const int MaxCandidates = 4;

    /// <summary>The site's home page over https, from whatever the Website column holds — or null
    /// for anything that is not a plain host name (an IP literal, "localhost", a name without a dot).
    /// http is upgraded rather than refused here: the column records where the company is, not how
    /// we will talk to it, and the fetch itself only ever goes out over https.</summary>
    public static Uri? HomePage(string? website)
    {
        if (website is null || !Uri.TryCreate(website.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.HostNameType != UriHostNameType.Dns || !uri.Host.Contains('.')
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return new Uri($"https://{uri.IdnHost}/");
    }

    public static IReadOnlyList<Uri> Candidates(string html, Uri pageUri)
    {
        var found = new List<(Uri Uri, int Rank, int Size)>();
        foreach (Match link in LinkTagRegex().Matches(html))
        {
            var tag = link.Value;
            var rel = Attribute(tag, "rel")?.ToLowerInvariant();
            if (rel is null)
            {
                continue;
            }

            var tokens = rel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rank = tokens.Contains("apple-touch-icon") || tokens.Contains("apple-touch-icon-precomposed") ? 0
                : tokens.Contains("icon") ? 1
                : -1;
            if (rank < 0)
            {
                continue;
            }

            var type = Attribute(tag, "type")?.ToLowerInvariant();
            if (type is not null && (type.Contains("svg") || type.Contains("icon")))
            {
                continue;
            }

            var href = Attribute(tag, "href");
            if (href is null || !Uri.TryCreate(pageUri, href, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                continue;
            }

            var path = uri.AbsolutePath.ToLowerInvariant();
            if (path.EndsWith(".svg", StringComparison.Ordinal) || path.EndsWith(".ico", StringComparison.Ordinal))
            {
                continue;
            }

            found.Add((uri, rank, LargestSize(Attribute(tag, "sizes"), rank)));
        }

        var ordered = found
            .OrderBy(c => c.Rank)
            .ThenByDescending(c => c.Size)
            .Select(c => c.Uri)
            .Distinct()
            .ToList();

        // The conventional location, for the many sites that serve one without declaring it.
        var conventional = new Uri(pageUri, "/apple-touch-icon.png");
        if (!ordered.Contains(conventional))
        {
            ordered.Add(conventional);
        }

        return ordered.Take(MaxCandidates).ToList();
    }

    /// <summary>The largest side a <c>sizes</c> attribute names ("32x32 180x180" → 180). None
    /// declared: an apple-touch-icon is assumed at its usual 180, a plain icon at a favicon's 32.</summary>
    private static int LargestSize(string? sizes, int rank)
    {
        var largest = 0;
        foreach (Match size in SizeRegex().Matches(sizes ?? string.Empty))
        {
            if (int.TryParse(size.Groups[1].Value, out var side))
            {
                largest = Math.Max(largest, side);
            }
        }

        return largest > 0 ? largest : rank == 0 ? 180 : 32;
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\b{name}\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Success ? match.Groups[1].Value
            : match.Groups[2].Success ? match.Groups[2].Value
            : match.Groups[3].Value;
        value = WebUtility.HtmlDecode(value).Trim();
        return value.Length == 0 ? null : value;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"(\d{1,4})\s*[xX]\s*\d{1,4}")]
    private static partial Regex SizeRegex();
}
