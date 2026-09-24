using System.Text.RegularExpressions;
using Sentry;

namespace AfterApply.Api.Telemetry;

/// <summary>
/// Keeps credentials that travel in URLs out of Sentry — the same list as the web's
/// lib/privacy/urlSecrets.ts. On this side the one that actually arrives in a query string is the
/// progress hub's ticket (<c>?access_token=</c>, HubTicketDefaults); the rest are listed so a
/// future route that takes one is covered before anyone remembers to add it.
/// </summary>
public static partial class SentryScrubbing
{
    public const string Filtered = "[Filtered]";

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        var request = sentryEvent.Request;
        request.Url = ScrubUrl(request.Url);
        request.QueryString = ScrubUrl(request.QueryString);
        if (request.Headers.TryGetValue("Referer", out var referer))
        {
            request.Headers["Referer"] = ScrubUrl(referer)!;
        }

        return sentryEvent;
    }

    public static Breadcrumb Scrub(Breadcrumb breadcrumb)
    {
        if (breadcrumb.Data is not { Count: > 0 } data || !data.Keys.Any(k => k is "url" or "from" or "to"))
        {
            return breadcrumb;
        }

        var scrubbed = data.ToDictionary(kv => kv.Key, kv => kv.Key is "url" or "from" or "to" ? ScrubUrl(kv.Value)! : kv.Value);
        return new Breadcrumb(breadcrumb.Message, breadcrumb.Type, scrubbed, breadcrumb.Category, breadcrumb.Level);
    }

    /// <summary>The value of every sensitive parameter replaced, wherever in the text it sits: a
    /// full URL, a bare query string, with or without "?".</summary>
    public static string? ScrubUrl(string? value) =>
        string.IsNullOrEmpty(value) ? value : SensitiveParameter().Replace(value, m => $"{m.Groups["key"].Value}={Filtered}");

    [GeneratedRegex(@"(?<=^|[?&#])(?<key>token|access_token|id_token|ticket|code|state|email)=[^&#]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveParameter();
}
