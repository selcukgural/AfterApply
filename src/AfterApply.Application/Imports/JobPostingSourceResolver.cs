using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports;

/// <summary>
/// Classifies a job posting URL (as captured by the browser extension's "I Applied" flow) by the
/// site it was scraped from, so <c>Job.Source</c> reflects actual provenance instead of being
/// hardcoded to a single supported site, and pairs it with that site's <c>Job.ExternalId</c>
/// extractor. Unrecognized hosts (or an unparsable URL) fall back to <see cref="Source.Other"/>
/// with no external id — the extension's own <c>host_permissions</c> restrict which sites it can
/// actually run on, so this fallback only guards against a future site being added to the
/// extension without a matching case here. Pure function — no I/O.
/// </summary>
public static class JobPostingSourceResolver
{
    public static (Source Source, string? ExternalId) Resolve(string jobUrl)
    {
        if (Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri))
        {
            if (IsHost(uri, "linkedin.com"))
            {
                return (Source.LinkedIn, LinkedInJobIdExtractor.Extract(jobUrl));
            }

            if (IsHost(uri, "kariyer.net"))
            {
                return (Source.KariyerNet, KariyerNetJobIdExtractor.Extract(jobUrl));
            }
        }

        return (Source.Other, null);
    }

    private static bool IsHost(Uri uri, string domain) =>
        uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
}
