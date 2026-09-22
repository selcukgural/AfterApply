namespace AfterApply.Application.Common;

/// <summary>
/// The one place that answers "is this URL on a host we allow?". Every outbound fetch in the
/// product (company enrichment, job link preview, the job-source sweep, the ATS job enrichment)
/// and every client-supplied URL that one of those later fetches is gated on this check, so the
/// comparison has to be the same everywhere — a subdomain-suffix match, never
/// <c>Contains</c>/<c>StartsWith</c>, or <c>notkariyer.net</c> and <c>kariyer.net.evil.com</c>
/// both slip through and turn a background fetch into an SSRF vector.
///
/// This used to be copy-pasted into six classes (the extension validator, CompanyEnrichmentService,
/// JobLinkPreviewService, both job-source clients and JobPostingSourceResolver). It was pulled out
/// when the allow-list grew past two sites: a subtle divergence between six copies is exactly the
/// kind of hole nobody notices. Pure functions — no I/O.
/// </summary>
public static class HostRules
{
    /// <summary>The host is <paramref name="domain"/> itself or one of its subdomains. Scheme is
    /// not considered — callers that fetch the URL must use <see cref="IsHttpsHost(Uri, string[])"/>
    /// instead. Used by classifiers that only need to know which site a URL belongs to.</summary>
    public static bool IsHost(Uri uri, string domain) =>
        uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    /// <summary>https, and on one of <paramref name="domains"/>. This is the shape every
    /// fetch-time and redirect-hop check wants: plain http is refused outright rather than
    /// upgraded, because a URL we were handed over http is a URL someone else could have
    /// rewritten.</summary>
    public static bool IsHttpsHost(Uri uri, params string[] domains) =>
        uri.Scheme == Uri.UriSchemeHttps && domains.Any(domain => IsHost(uri, domain));

    /// <summary>Validator-facing overload: parses first, so a malformed or relative URL is a
    /// refusal rather than an exception. A null/empty value is <c>false</c> — call sites make the
    /// field optional with FluentValidation's <c>When(...)</c>, they do not rely on this returning
    /// true for "nothing supplied".</summary>
    public static bool IsHttpsUrlOnAllowedHost(string? url, params string[] domains) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && IsHttpsHost(uri, domains);
}
