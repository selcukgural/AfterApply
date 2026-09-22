using System.Text.RegularExpressions;
using AfterApply.Application.Common;
using AfterApply.Domain.Common;

namespace AfterApply.Application.AtsSources;

/// <summary>
/// Builds the one URL the ATS enrichment fetch is allowed to request, and is therefore the SSRF
/// boundary for that fetch — same role <c>LinkedInJobSearchUrlBuilder</c> plays for the sweep.
/// Nothing a client sent is concatenated in raw: the host is a constant, and each segment of the
/// stored <c>ExternalId</c> has to match a narrow shape before it is escaped into the path. A
/// segment that does not is not "sanitized", it is a refusal.
///
/// Endpoints (all public, no authentication, verified live 2026-09-22):
/// <list type="bullet">
/// <item>Greenhouse — <c>boards-api.greenhouse.io/v1/boards/{board}/jobs/{id}?content=true</c></item>
/// <item>Lever — <c>api.lever.co/v0/postings/{site}/{id}?mode=json</c></item>
/// <item>Ashby — <c>api.ashbyhq.com/posting-api/job-board/{org}</c> (whole board; the posting is
///   picked out of it by id, because Ashby publishes no single-posting endpoint)</item>
/// <item>SmartRecruiters — <c>api.smartrecruiters.com/v1/companies/{company}/postings/{id}</c></item>
/// <item>Workday — <c>{tenant}.wd{n}.myworkdayjobs.com/wday/cxs/{tenant}/{site}/job/...</c>, derived
///   from the posting's own page URL rather than from the external id, because the career-site
///   name and location path are part of the address and are not in the id</item>
/// <item>Workable — <c>apply.workable.com/api/v1/widget/accounts/{account}?details=true</c> (whole
///   board again, the posting picked out by shortcode). <c>details=true</c> is the whole of it:
///   without that parameter the response carries the company and a list of job titles and no
///   description at all, which is what an earlier reading of this endpoint saw, and why 0.9.0
///   shipped with Workable deliberately unsupported (DECISIONS.md 2026-09-22). With it, each row
///   carries the posting's HTML description, city/state/country, employment type and publish
///   date — measured live 2026-09-22 against a board with seven open jobs.</item>
/// </list>
/// Pure functions — no I/O.
/// </summary>
public static partial class AtsApiUrlBuilder
{
    /// <summary>The API hosts the enrichment client may talk to — a separate, tighter list than
    /// the page hosts in <c>JobPostingSourceResolver.AtsDomains</c>, since these are the ones a
    /// redirect is allowed to stay on.</summary>
    public static readonly string[] ApiDomains =
    [
        "greenhouse.io", "lever.co", "ashbyhq.com", "smartrecruiters.com",
        "myworkdayjobs.com", "myworkdaysite.com", "workable.com"
    ];

    public static Uri? Build(Source source, string jobUrl, string externalId)
    {
        if (source == Source.Workday)
        {
            return BuildWorkday(jobUrl);
        }

        var parts = externalId.Split('/');
        if (parts.Length != 2 || !AccountRegex().IsMatch(parts[0]))
        {
            return null;
        }

        var account = Uri.EscapeDataString(parts[0]);
        var posting = parts[1];

        return source switch
        {
            Source.Greenhouse when NumericRegex().IsMatch(posting) =>
                new Uri($"https://boards-api.greenhouse.io/v1/boards/{account}/jobs/{posting}?content=true"),
            Source.Lever when UuidRegex().IsMatch(posting) =>
                new Uri($"https://api.lever.co/v0/postings/{account}/{posting}?mode=json"),
            Source.Ashby when UuidRegex().IsMatch(posting) =>
                new Uri($"https://api.ashbyhq.com/posting-api/job-board/{account}"),
            Source.SmartRecruiters when NumericRegex().IsMatch(posting) =>
                new Uri($"https://api.smartrecruiters.com/v1/companies/{account}/postings/{posting}"),
            Source.Workable when ShortcodeRegex().IsMatch(posting) =>
                new Uri($"https://apply.workable.com/api/v1/widget/accounts/{account}?details=true"),
            _ => null
        };
    }

    // The cxs address mirrors the page address exactly, minus the optional locale segment:
    //   page  https://nvidia.wd5.myworkdayjobs.com/en-US/Site/job/China-Shenzhen/Manager_JR2026166
    //   api   https://nvidia.wd5.myworkdayjobs.com/wday/cxs/nvidia/Site/job/China-Shenzhen/Manager_JR2026166
    // On myworkdaysite.com the tenant is in the path ("/recruiting/{tenant}/...") instead of the
    // host, and drops out of the remainder the same way the locale does.
    private static Uri? BuildWorkday(string jobUrl)
    {
        if (!Uri.TryCreate(jobUrl, UriKind.Absolute, out var uri)
            || !HostRules.IsHttpsHost(uri, "myworkdayjobs.com", "myworkdaysite.com"))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segments.Count > 0 && LocaleRegex().IsMatch(segments[0]))
        {
            segments.RemoveAt(0);
        }

        string tenant;
        if (HostRules.IsHost(uri, "myworkdaysite.com"))
        {
            if (segments.Count < 2 || !segments[0].Equals("recruiting", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            tenant = segments[1];
            segments.RemoveRange(0, 2);
        }
        else
        {
            tenant = uri.Host.Split('.')[0];
        }

        // "{site}/job/{location}/{slug}" — anything shorter is a listing page, not a posting.
        if (!AccountRegex().IsMatch(tenant) || segments.Count < 4
            || !segments[1].Equals("job", StringComparison.OrdinalIgnoreCase)
            || segments.Any(segment => !PathSegmentRegex().IsMatch(segment)))
        {
            return null;
        }

        return new Uri($"https://{uri.Host}/wday/cxs/{tenant}/{string.Join('/', segments)}");
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")]
    private static partial Regex AccountRegex();

    [GeneratedRegex(@"^\d{1,20}$")]
    private static partial Regex NumericRegex();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRegex();

    // Workable's own shortcode: uppercase hex in practice, matched case-insensitively because it
    // is copied out of whatever casing the page URL carried.
    [GeneratedRegex(@"^[0-9A-Fa-f]{8,32}$")]
    private static partial Regex ShortcodeRegex();

    [GeneratedRegex(@"^[a-z]{2}(-[A-Za-z]{2,4})?$")]
    private static partial Regex LocaleRegex();

    // No "..", no escaping out of the path, no encoded separators — the remainder is copied from
    // the page URL, so it has to be proven safe rather than assumed to be.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._\-()]{0,199}$")]
    private static partial Regex PathSegmentRegex();
}
