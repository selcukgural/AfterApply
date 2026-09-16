using System.Globalization;
using System.Text;

namespace AfterApply.Application.JobSources;

/// <summary>
/// The only place a kariyer.net search or posting URL is built (verified live 2026-09-14). The
/// listing is server-rendered at <c>/is-ilanlari/{city}?kw={keywords}</c>; the site answers with
/// a 301 onto the same path plus its own city ids (<c>ct=34,82</c>), which the client follows. Page
/// <c>n</c> is <c>/is-ilanlari/{city}-{n}?kw=…&amp;cp={n}</c>. A posting is reachable by its id
/// alone (<c>/is-ilani/{id}</c>, 301 to the canonical slug URL), so the id is all that is stored
/// for the detail fetch, as with LinkedIn.
///
/// The city goes into the path as a slug — ASCII letters, digits and dashes only — and the
/// keywords go through <see cref="Uri.EscapeDataString"/>; nothing a user types can steer the
/// fetch off these two paths (SSRF). kariyer.net's robots.txt (read the same day) allows
/// <c>/is-ilanlari</c> and <c>/is-ilani</c>; it disallows <c>/filtre</c>, which is never used.
/// </summary>
public static class KariyerNetJobSearchUrlBuilder
{
    public const string Host = "www.kariyer.net";
    public const string SearchPath = "/is-ilanlari/";
    public const string PostingPath = "/is-ilani/";

    /// <summary>The listing shows about fifty cards a page; the client stops on an empty page or
    /// when the page has no "next" link, so the number is only a sanity ceiling.</summary>
    public const int MaxCardsPerPage = 60;

    /// <param name="page">1-based.</param>
    public static Uri Search(string keywords, string location, int page)
    {
        var slug = Slugify(location);
        if (slug.Length == 0)
        {
            throw new ArgumentException("The location has no characters a slug can carry.", nameof(location));
        }

        var pageNumber = page.ToString(CultureInfo.InvariantCulture);
        var path = page <= 1 ? SearchPath + slug : SearchPath + slug + "-" + pageNumber;
        var query = "?kw=" + Uri.EscapeDataString(keywords) + (page <= 1 ? string.Empty : "&cp=" + pageNumber);
        return new Uri("https://" + Host + path + query);
    }

    public static Uri Posting(string externalId)
    {
        if (externalId.Length == 0 || !externalId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A kariyer.net posting id is numeric.", nameof(externalId));
        }

        return new Uri("https://" + Host + PostingPath + externalId);
    }

    /// <summary>Whether a URL the site redirected to is still the search we asked for: same
    /// host, the listing path, and the city slug still in it. An unknown city is answered with a
    /// redirect to the bare nationwide listing, which is not an answer to the user's question.</summary>
    public static bool IsSearchFor(Uri uri, string location)
    {
        if (!uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals("kariyer.net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var slug = Slugify(location);
        var path = uri.AbsolutePath;
        return path.StartsWith(SearchPath + slug, StringComparison.OrdinalIgnoreCase)
               && (path.Length == SearchPath.Length + slug.Length || path[SearchPath.Length + slug.Length] == '-');
    }

    /// <summary>"İstanbul (Avrupa)" → "istanbul-avrupa": Turkish letters folded to ASCII the way
    /// the site's own slugs are, everything else to a dash, runs and ends trimmed.</summary>
    public static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasDash = true;
        foreach (var raw in value.Trim())
        {
            // A combining mark (the dot a decomposed "i̇" carries) is not a letter and not a gap.
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = raw switch
            {
                'ı' or 'I' or 'İ' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ç' or 'Ç' => 'c',
                'ö' or 'Ö' => 'o',
                'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(raw)
            };

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
