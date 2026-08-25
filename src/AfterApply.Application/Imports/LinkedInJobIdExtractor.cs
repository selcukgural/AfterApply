using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// Extracts the numeric LinkedIn job posting id embedded in a job URL (e.g.
/// <c>https://www.linkedin.com/jobs/view/4449445627/?...</c> → <c>"4449445627"</c>),
/// for use as <c>Job.ExternalId</c> (spec §7: <c>Source = LinkedIn, ExternalId = 4449445627</c>).
/// Pure function — no I/O.
/// </summary>
public static partial class LinkedInJobIdExtractor
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

    [GeneratedRegex(@"/jobs/view/(\d+)")]
    private static partial Regex JobIdRegex();
}
