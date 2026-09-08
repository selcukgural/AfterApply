using AfterApply.Domain.Common;

namespace AfterApply.Domain.SiteTraffic;

/// <summary>
/// One day's count for one (event, page, language, referring site) combination on the public site.
///
/// **This table is the whole design, so what it deliberately does not hold matters more than what
/// it does.** There is no visitor id, no session id, no cookie behind it, no IP address and no user
/// agent — a row cannot be traced to a person, and two visits by the same person are
/// indistinguishable from two visits by two people. That is not a limitation we ran into, it is the
/// requirement: the published Çerez Politikası states that the site carries no third-party
/// analytics and no tracking, and promises that adding any would come with a consent screen first.
/// A first-party counter that stores no identifier keeps that promise intact, which is why the
/// funnel is measured as a ratio of daily totals rather than by following anyone through it.
///
/// Consequence to keep in mind when reading the numbers: "40 landing views and 1 registration
/// today" is a ratio of two independent counts, not a tracked journey. At this product's traffic
/// that is enough to answer the only question being asked — is anybody arriving, and does anybody
/// continue — and it is the honest limit of what may be collected without asking.
///
/// Not user-owned: no UserId, so this table is outside the cascade-from-Users rule that every
/// user-owned table follows (DECISIONS.md 2026-09-07). Deleting an account cannot change these
/// rows, because nothing in them ever referred to an account.
/// </summary>
public sealed class SiteTrafficDailyCounter : Entity
{
    /// <summary>The UTC day being counted. Part of the upsert key.</summary>
    public DateOnly Day { get; private set; }

    public SiteTrafficEvent Event { get; private set; }

    /// <summary>Normalised, language-prefix removed, query string discarded, and matched against a
    /// fixed allowlist of public routes — see SiteTrafficNormalizer. Never a signed-in page.</summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>"tr" or "en", taken from the path's language prefix.</summary>
    public string Locale { get; private set; } = string.Empty;

    /// <summary>Host only — never the referring path or its query, which is where a search term or
    /// someone's name would sit. Empty means the visit had no referrer (typed, bookmarked, or the
    /// referrer was suppressed).</summary>
    public string ReferrerHost { get; private set; } = string.Empty;

    public int Count { get; private set; }

    /// <summary>When this combination was last seen. Kept for one reason: a stale row is how you
    /// notice the reporter stopped firing after a deploy, which otherwise reads as "traffic
    /// dropped".</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    private SiteTrafficDailyCounter()
    {
    }

    public static SiteTrafficDailyCounter Create(
        DateOnly day, SiteTrafficEvent trafficEvent, string path, string locale, string referrerHost,
        DateTimeOffset seenAt)
    {
        return new SiteTrafficDailyCounter
        {
            Day = day,
            Event = trafficEvent,
            Path = path,
            Locale = locale,
            ReferrerHost = referrerHost,
            Count = 1,
            LastSeenAt = seenAt
        };
    }
}
