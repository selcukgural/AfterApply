using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// Extracts the numeric kariyer.net job posting id embedded in a job URL (e.g.
/// <c>https://www.kariyer.net/is-ilani/acme-backend-engineer-4539310</c> → <c>"4539310"</c>),
/// for use as <c>Job.ExternalId</c>. kariyer.net always appends the numeric ilan id as the final
/// <c>-&lt;digits&gt;</c> segment of the slug, after the company/title text — the greedy
/// <c>[^/?#]*</c> below backtracks to that trailing occurrence even when the slug itself contains
/// other digits (e.g. a "5651 kanun" regulation reference in a company blurb). Pure function — no
/// I/O.
/// </summary>
public static partial class KariyerNetJobIdExtractor
{
    public static string? Extract(string? jobUrl)
    {
        if (string.IsNullOrWhiteSpace(jobUrl))
        {
            return null;
        }

        var match = JobIdRegex().Match(jobUrl);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"/is-ilani/[^/?#]*-(\d+)(?:$|[/?#])")]
    private static partial Regex JobIdRegex();
}
