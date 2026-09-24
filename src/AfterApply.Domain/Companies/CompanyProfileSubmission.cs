using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

/// <summary>
/// One user's word on which profile page (LinkedIn or kariyer.net) is a company's, as their capture
/// pointed at it (2026-09-24). A company holds one link per platform and the first capture fills
/// it; these rows are how many different people have since pointed at the same page — the public
/// page shows the website read from that page only once that count reaches the threshold. One row
/// per (company, platform, user): a user's later capture replaces their earlier word, it does not
/// add to it.
/// </summary>
public sealed class CompanyProfileSubmission : Entity
{
    public Guid CompanyId { get; private set; }

    public Source Platform { get; private set; }

    /// <summary>Canonical form, see <see cref="CompanyProfileUrl.Canonical"/>.</summary>
    public string Url { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    private CompanyProfileSubmission()
    {
    }

    public static CompanyProfileSubmission Create(Guid companyId, Source platform, string url, Guid userId, DateTimeOffset now) =>
        new() { CompanyId = companyId, Platform = platform, Url = CompanyProfileUrl.Canonical(url), UserId = userId, SubmittedAt = now };

    public void Replace(string url, DateTimeOffset now)
    {
        Url = CompanyProfileUrl.Canonical(url);
        SubmittedAt = now;
    }
}

/// <summary>The one spelling of a profile URL two captures are compared in: lower-case scheme and
/// host without "www.", no query or fragment, no trailing slash. Two captures of the same page from
/// different tracking links therefore agree.</summary>
public static class CompanyProfileUrl
{
    public static string Canonical(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return url.Trim().ToLowerInvariant();
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return $"{uri.Scheme.ToLowerInvariant()}://{host}{uri.AbsolutePath.TrimEnd('/').ToLowerInvariant()}";
    }
}
