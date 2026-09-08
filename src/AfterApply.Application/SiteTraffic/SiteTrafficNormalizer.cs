using System.Text.RegularExpressions;
using AfterApply.Domain.SiteTraffic;

namespace AfterApply.Application.SiteTraffic;

/// <summary>What a reported event becomes once it has been cleaned up and accepted.</summary>
public sealed record NormalizedSiteTrafficEvent(
    SiteTrafficEvent Event, string Path, string Locale, string ReferrerHost);

/// <summary>
/// Turns whatever a browser posted into the small, closed vocabulary the counter table stores — or
/// into nothing at all.
///
/// This class is where the privacy promise is actually kept, so it is deliberately strict and it is
/// pure (no database, no clock, no HTTP) so the rules can be tested one by one. Three of them carry
/// the weight:
///
/// 1. **The query string is discarded before anything else.** It is the one part of a URL that
///    routinely carries personal data — a reset-password token, an OAuth code, a search term
///    someone typed. It is cut off, not parsed.
/// 2. **Paths are matched against an allowlist, never merely sanitised.** Anything that is not a
///    known public page — every signed-in page included — is dropped. So an application id can
///    never reach this table even if a caller posts one, and the table's row count stays bounded by
///    the number of pages the site has rather than by what callers invent.
/// 3. **A referrer is reduced to its host.** The referring path and query are where a search
///    engine puts the query and where a forum puts the thread title; neither is kept.
///
/// A rejected report is not an error. <see cref="Normalize"/> returns null and the endpoint answers
/// 204: telling a caller which shapes are accepted would hand a scraper the map, and a browser has
/// nothing useful to do with the refusal anyway.
/// </summary>
public static class SiteTrafficNormalizer
{
    public const int MaxPathLength = 512;
    public const int MaxReferrerLength = 512;
    public const int MaxEventNameLength = 64;

    private const int MaxHostLength = 100;

    /// <summary>The site's languages. Hardcoded rather than read from the web app's routing config
    /// because the Application layer does not depend on the frontend; the two must be changed
    /// together, which a test asserts.</summary>
    private static readonly HashSet<string> Locales = new(StringComparer.Ordinal) { "tr", "en" };

    /// <summary>Wire names are snake_case so the frontend can pass a literal; the enum is the
    /// storage form. Unknown names are dropped rather than recorded as "other" — an "other" bucket
    /// grows silently and tells you nothing about what is in it.</summary>
    private static readonly Dictionary<string, SiteTrafficEvent> EventsByWireName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["page_view"] = SiteTrafficEvent.PageView,
            ["cta_get_started"] = SiteTrafficEvent.CtaGetStarted,
            ["register_started"] = SiteTrafficEvent.RegisterStarted,
            ["register_completed"] = SiteTrafficEvent.RegisterCompleted
        };

    /// <summary>
    /// Public pages, with the language prefix already removed. The OAuth callback routes
    /// (/auth/google/callback and friends) are missing on purpose and must stay missing: they carry
    /// an authorization code in the query, they say nothing about acquisition, and the safest way
    /// to never store one is to never accept the path it lives on.
    /// </summary>
    private static readonly HashSet<string> ExactPaths = new(StringComparer.Ordinal)
    {
        "/",
        "/login",
        "/register",
        "/forgot-password",
        "/reset-password",
        "/guide",
        "/help",
        "/privacy",
        "/cookies",
        "/extension-privacy"
    };

    /// <summary>Sections with one page per slug. The slug itself is what the guide section exists to
    /// measure — which article brings anyone in — so it is kept, bounded by
    /// <see cref="SlugPattern"/> rather than by a list the Application layer would have to keep in
    /// step with the MDX files.</summary>
    private static readonly HashSet<string> SlugSections = new(StringComparer.Ordinal) { "/guide", "/help" };

    private static readonly Regex SlugPattern =
        new("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HostPattern =
        new("^[a-z0-9][a-z0-9.-]{0,98}[a-z0-9]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <returns>The cleaned event, or null when the report is to be accepted and discarded.</returns>
    public static NormalizedSiteTrafficEvent? Normalize(string? eventName, string? path, string? referrer)
    {
        if (eventName is null || eventName.Length > MaxEventNameLength
            || !EventsByWireName.TryGetValue(eventName.Trim(), out var trafficEvent))
        {
            return null;
        }

        var (locale, normalizedPath) = ResolvePath(path);
        if (locale is null || normalizedPath is null)
        {
            return null;
        }

        return new NormalizedSiteTrafficEvent(trafficEvent, normalizedPath, locale, ResolveReferrerHost(referrer));
    }

    private static (string? Locale, string? Path) ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength)
        {
            return (null, null);
        }

        var trimmed = path.Trim();

        // Cut the query and fragment off before looking at anything. Everything sensitive a URL can
        // carry lives after one of these two characters.
        var cut = trimmed.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            trimmed = trimmed[..cut];
        }

        if (!trimmed.StartsWith('/'))
        {
            return (null, null);
        }

        // ToLowerInvariant, never ToLower(): this product's default culture is tr-TR, where "I"
        // lowercases to a dotless "ı" and would turn a valid path into an unmatchable one.
        var segments = trimmed.ToLowerInvariant()
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || !Locales.Contains(segments[0]))
        {
            // Every page on this site is served under a language prefix (localePrefix: "always"), so
            // a path without one did not come from a real page view.
            return (null, null);
        }

        var locale = segments[0];
        var rest = segments[1..];

        var candidate = rest.Length == 0 ? "/" : "/" + string.Join('/', rest);

        if (ExactPaths.Contains(candidate))
        {
            return (locale, candidate);
        }

        if (rest.Length == 2 && SlugSections.Contains("/" + rest[0]) && SlugPattern.IsMatch(rest[1]))
        {
            return (locale, candidate);
        }

        // Not a public page — a signed-in screen, a static file, or something invented. Dropped.
        return (null, null);
    }

    private static string ResolveReferrerHost(string? referrer)
    {
        if (string.IsNullOrWhiteSpace(referrer) || referrer.Length > MaxReferrerLength
                                                || !Uri.TryCreate(referrer.Trim(), UriKind.Absolute, out var uri)
                                                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return string.Empty;
        }

        var host = uri.Host.ToLowerInvariant();

        // "www.google.com" and "google.com" are the same source; splitting them would split the
        // count for no reason.
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host.Length <= MaxHostLength && HostPattern.IsMatch(host) ? host : string.Empty;
    }
}
