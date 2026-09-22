using AfterApply.Application.Common;
using AfterApply.Domain.Common;

namespace AfterApply.Application.Imports;

/// <summary>
/// Classifies a job posting URL (as captured by the browser extension's "I Applied" flow) by the
/// site it was scraped from, so <c>Job.Source</c> reflects actual provenance instead of being
/// hardcoded to a single supported site, and pairs it with that site's <c>Job.ExternalId</c>
/// extractor.
///
/// The <see cref="Source.Other"/> fallback is a real, everyday outcome rather than a guard against
/// a future mistake: since 0.9.0 the extension also captures from pages it has no adapter for
/// (any page carrying schema.org JobPosting markup, plus the "add it by hand" path), so an
/// unrecognized host is exactly what that produces. Such a job gets no external id, which means
/// JobResolver always inserts a fresh row for it — correct, because we have no per-site identity
/// to dedupe two captures of the same posting against. <c>Application.JobUrl</c> still dedupes
/// per user, which is the boundary that actually matters to the person.
///
/// The table below is the single place a new capture target is registered on the backend; its
/// client-side twin is <c>ADAPTERS</c> in <c>extension/adapters.js</c>. Pure function — no I/O.
/// </summary>
public static class JobPostingSourceResolver
{
    private static readonly (string Domain, Source Source, Func<string, string?> ExternalId)[] KnownSites =
    [
        ("linkedin.com", Source.LinkedIn, LinkedInJobIdExtractor.Extract),
        ("kariyer.net", Source.KariyerNet, KariyerNetJobIdExtractor.Extract),
        ("greenhouse.io", Source.Greenhouse, AtsJobIdExtractor.Greenhouse),
        ("lever.co", Source.Lever, AtsJobIdExtractor.Lever),
        ("ashbyhq.com", Source.Ashby, AtsJobIdExtractor.Ashby),
        ("myworkdayjobs.com", Source.Workday, AtsJobIdExtractor.Workday),
        ("myworkdaysite.com", Source.Workday, AtsJobIdExtractor.Workday),
        ("workable.com", Source.Workable, AtsJobIdExtractor.Workable),
        ("smartrecruiters.com", Source.SmartRecruiters, AtsJobIdExtractor.SmartRecruiters)
    ];

    public static (Source Source, string? ExternalId) Resolve(string jobUrl)
    {
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri))
        {
            return (Source.Other, null);
        }

        foreach (var (domain, source, externalId) in KnownSites)
        {
            if (HostRules.IsHost(uri, domain))
            {
                return (source, externalId(jobUrl));
            }
        }

        return (Source.Other, null);
    }

    /// <summary>The hosts a client may hand us a company-level ATS link for. Kept next to the
    /// table it mirrors so the two cannot drift; consumed by
    /// <c>CreateFromExtensionRequestValidator</c>, which pins <c>CompanyAtsUrl</c> to it because
    /// the enrichment job later fetches that URL server-side (SSRF).</summary>
    public static readonly string[] AtsDomains =
    [
        "greenhouse.io", "lever.co", "ashbyhq.com",
        "myworkdayjobs.com", "myworkdaysite.com", "workable.com", "smartrecruiters.com"
    ];

    /// <summary>True for the six ATS members added in 0.9.0 — the sources whose postings can be
    /// re-read from a public, unauthenticated API (see <c>AtsSources</c>).</summary>
    public static bool IsAts(Source source) =>
        source is Source.Greenhouse or Source.Lever or Source.Ashby
            or Source.Workday or Source.Workable or Source.SmartRecruiters;
}
