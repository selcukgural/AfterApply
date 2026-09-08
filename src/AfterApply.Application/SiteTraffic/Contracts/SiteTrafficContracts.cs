namespace AfterApply.Application.SiteTraffic.Contracts;

/// <summary>
/// What the public site posts. Three fields, and none of them identifies anyone: the language comes
/// out of <paramref name="Path"/>'s prefix rather than being asked for separately, and there is
/// deliberately no visitor id, session id or timestamp field — the server supplies the day, and a
/// caller-supplied identifier is exactly what this feature refuses to hold.
/// </summary>
/// <param name="Event">One of the wire names in SiteTrafficNormalizer; anything else is discarded.</param>
/// <param name="Path">The page's path including its language prefix. The query string may be
/// present — the server cuts it off before looking at the rest — but sending it is pointless.</param>
/// <param name="Referrer">document.referrer, or null. Reduced to a host on arrival.</param>
public sealed record RecordSiteTrafficEventRequest(string? Event, string? Path, string? Referrer);

/// <summary>One stored counter row, as the admin view reads it.</summary>
public sealed record SiteTrafficCounterResponse(
    DateOnly Day, string Event, string Path, string Locale, string ReferrerHost, int Count);
