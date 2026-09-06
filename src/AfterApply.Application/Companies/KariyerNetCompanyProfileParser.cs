using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AfterApply.Application.Companies;

/// <summary>
/// Extracts company-profile fields from a kariyer.net /firma-profil/ page's server-rendered HTML —
/// the counterpart to <see cref="LinkedInCompanyProfileParser"/>, and the only enrichment source
/// for a company that has only ever been seen in a kariyer.net posting. Pure function, no I/O; the
/// caller fetches the (size-capped) HTML.
///
/// Verified empirically against live pages on 2026-09-06 (plain HTTP GET, honest bot User-Agent,
/// logged out — same technique already validated for job postings): every job posting links to its
/// company's profile page, and roughly 44% of those profiles publish the company's own site. The
/// rest publish none at all, which is why a null return here is the normal case, not a failure.
///
/// The two fields come from two different places on purpose:
///
/// <list type="bullet">
/// <item>The website is read from the rendered anchor, whose <c>title</c> is the company name
/// followed by "Web Sitesi". kariyer.net is a Turkish-only site, so that suffix is stable in a way
/// a translated label would not be, and it identifies the link unambiguously among the social
/// icons sitting next to it.</item>
/// <item>The sector has no rendered representation at all — it exists only inside the Nuxt
/// hydration payload, which is not ordinary JSON: it is a flat array where every object field
/// holds an *index* into that same array rather than a value (Nuxt's devalue encoding). So
/// <c>{"companySectors": 86}</c> means "entry 86", which is <c>[87]</c>, which is
/// <c>{"sectorName": 46}</c>, which is finally the string at entry 46. Hence the index-chasing
/// below — a plain deserialize would hand back integers.</item>
/// </list>
/// </summary>
public static partial class KariyerNetCompanyProfileParser
{
    /// <summary>The company's own site, or null when the profile publishes none (the common case).</summary>
    public static string? ExtractWebsite(string html)
    {
        foreach (Match anchor in AnchorTagRegex().Matches(html))
        {
            var tag = anchor.Value;
            if (!WebsiteTitleRegex().IsMatch(tag))
            {
                continue;
            }

            var href = HrefAttributeRegex().Match(tag);
            if (!href.Success)
            {
                continue;
            }

            var url = WebUtility.HtmlDecode(href.Groups[1].Value).Trim();

            // The value is copied from a third-party page straight into a column we later render
            // as a link, so anything that is not a plain http(s) URL is dropped here rather than
            // stored and dealt with at render time.
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? url
                : null;
        }

        return null;
    }

    /// <summary>
    /// The company's sector ("Lojistik", "Bilişim", ...) — fills the same Company.Industry column
    /// that LinkedIn's "Industry" row does, so a kariyer.net-only company is not left behind.
    /// </summary>
    public static string? ExtractSector(string html)
    {
        var payload = HydrationPayloadRegex().Match(html);
        if (!payload.Success)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload.Groups[1].Value);
            var entries = document.RootElement;
            if (entries.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            // The holder is whichever entry carries the company-profile shape. Found by property
            // rather than by a fixed index: the index shifts with page content.
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("companySectors", out var sectorsRef))
                {
                    continue;
                }

                var sectors = Dereference(entries, sectorsRef);
                if (sectors is not { ValueKind: JsonValueKind.Array } || sectors.Value.GetArrayLength() == 0)
                {
                    return null;
                }

                var sector = Dereference(entries, sectors.Value[0]);
                if (sector is not { ValueKind: JsonValueKind.Object }
                    || !sector.Value.TryGetProperty("sectorName", out var nameRef))
                {
                    return null;
                }

                var name = Dereference(entries, nameRef);
                var text = name?.ValueKind == JsonValueKind.String ? name.Value.GetString()?.Trim() : null;
                return string.IsNullOrEmpty(text) ? null : text;
            }
        }
        catch (JsonException)
        {
            // Truncated by the caller's size cap, or a payload format we no longer recognise —
            // either way the sector is simply unknown, never an enrichment failure.
            return null;
        }

        return null;
    }

    // Every value inside the payload is an index into the payload's own flat array. Out-of-range
    // (and the -1 that stands in for "absent") yields null rather than throwing.
    private static JsonElement? Dereference(JsonElement entries, JsonElement reference)
    {
        if (reference.ValueKind != JsonValueKind.Number || !reference.TryGetInt32(out var index))
        {
            return null;
        }

        return index >= 0 && index < entries.GetArrayLength() ? entries[index] : null;
    }

    [GeneratedRegex(@"<a\s[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex(@"title\s*=\s*""[^""]*Web\s+Sitesi\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex WebsiteTitleRegex();

    [GeneratedRegex(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttributeRegex();

    [GeneratedRegex(@"<script[^>]*id\s*=\s*[""']__NUXT_DATA__[""'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HydrationPayloadRegex();
}
