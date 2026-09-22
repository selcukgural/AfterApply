using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// Extracts <c>Job.ExternalId</c> from an applicant-tracking-system job URL. Unlike LinkedIn and
/// kariyer.net — where a single number identifies a posting globally — every ATS is multi-tenant
/// and numbers postings per customer, so the id here is always the composite
/// <c>"{account}/{posting}"</c> (e.g. <c>"stripe/4512345"</c>). Without the account segment two
/// different companies' postings could collide on the unique <c>(Source, ExternalId)</c> index in
/// JobConfiguration, silently merging one company's job into another's.
///
/// Kept as one class rather than six, because — unlike the two site-specific extractors next to
/// it — these six share that single rule and differ only in where the two segments sit in the URL.
///
/// Every method here has a twin in <c>extension/adapters.js</c>, which derives the same id
/// client-side so the canonical <c>JobUrl</c> the popup submits and the <c>ExternalId</c> this
/// derives stay in lockstep. Change one, change the other (and its test). Pure functions — no I/O.
/// </summary>
public static partial class AtsJobIdExtractor
{
    /// <summary><c>job-boards.greenhouse.io/{board}/jobs/{id}</c> (and the older
    /// <c>boards.greenhouse.io</c> host, still live on plenty of listings).</summary>
    public static string? Greenhouse(string? jobUrl) => Composite(jobUrl, GreenhouseRegex());

    /// <summary><c>jobs.lever.co/{site}/{uuid}</c>, optionally followed by <c>/apply</c> or
    /// <c>/thanks</c> — the post-submit page a candidate most often has open.</summary>
    public static string? Lever(string? jobUrl) => Composite(jobUrl, LeverRegex());

    /// <summary><c>jobs.ashbyhq.com/{org}/{uuid}</c>, optionally followed by
    /// <c>/application</c>.</summary>
    public static string? Ashby(string? jobUrl) => Composite(jobUrl, AshbyRegex());

    /// <summary>Two shapes for the same thing:
    /// <c>{tenant}.wd{n}.myworkdayjobs.com/[{locale}/]{site}/job/{location}/{slug}_{REQ}</c> and
    /// <c>wd{n}.myworkdaysite.com/[{locale}/]recruiting/{tenant}/{site}/job/...</c>. The
    /// requisition id is the trailing <c>_R-12345</c>/<c>_JR1234567</c> segment — the only part
    /// Workday itself treats as the posting's identity; the slug before it is SEO text that
    /// changes when the title is edited.</summary>
    public static string? Workday(string? jobUrl)
    {
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var requisition = WorkdayRequisitionRegex().Match(uri.AbsolutePath);
        if (!requisition.Success)
        {
            return null;
        }

        // myworkdaysite.com carries the tenant in the path (".../recruiting/{tenant}/..."),
        // myworkdayjobs.com in the first host label ("{tenant}.wd5.myworkdayjobs.com").
        var fromPath = WorkdayTenantInPathRegex().Match(uri.AbsolutePath);
        var tenant = fromPath.Success ? fromPath.Groups[1].Value : uri.Host.Split('.')[0];

        return string.IsNullOrEmpty(tenant) || tenant.StartsWith("wd", StringComparison.OrdinalIgnoreCase)
            ? null
            : $"{tenant}/{requisition.Groups[1].Value}";
    }

    /// <summary><c>apply.workable.com/{company}/j/{TOKEN}/</c> — the token is Workable's own
    /// uppercase hex shortcode, stable across the apply/confirmation steps.</summary>
    public static string? Workable(string? jobUrl) => Composite(jobUrl, WorkableRegex());

    /// <summary><c>jobs.smartrecruiters.com/{company}/{id}-{slug}</c> (also served from
    /// <c>careers.smartrecruiters.com</c>). The id is the long leading number; the slug after the
    /// first hyphen is title text.</summary>
    public static string? SmartRecruiters(string? jobUrl) => Composite(jobUrl, SmartRecruitersRegex());

    private static string? Composite(string? jobUrl, Regex regex)
    {
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var match = regex.Match(uri.AbsolutePath);
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : null;
    }

    [GeneratedRegex(@"^/([^/]+)/jobs/(\d+)(?:/|$)")]
    private static partial Regex GreenhouseRegex();

    [GeneratedRegex(@"^/([^/]+)/([0-9a-fA-F]{8}-[0-9a-fA-F-]{27,})(?:/|$)")]
    private static partial Regex LeverRegex();

    [GeneratedRegex(@"^/([^/]+)/([0-9a-fA-F]{8}-[0-9a-fA-F-]{27,})(?:/|$)")]
    private static partial Regex AshbyRegex();

    [GeneratedRegex(@"/job/[^/]+/[^/]*_([A-Za-z]{0,3}-?\d[A-Za-z0-9-]*)(?:/|$)")]
    private static partial Regex WorkdayRequisitionRegex();

    [GeneratedRegex(@"/recruiting/([^/]+)/")]
    private static partial Regex WorkdayTenantInPathRegex();

    [GeneratedRegex(@"^/([^/]+)/j/([0-9A-Fa-f]{8,})(?:/|$)")]
    private static partial Regex WorkableRegex();

    [GeneratedRegex(@"^/([^/]+)/(\d{6,})(?:-|/|$)")]
    private static partial Regex SmartRecruitersRegex();
}
