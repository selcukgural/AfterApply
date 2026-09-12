using System.Net;
using System.Text.RegularExpressions;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

/// <summary>
/// Reads the description and the four criteria (seniority, employment type, job function,
/// industries) out of LinkedIn's public posting fragment (<c>/jobs-guest/jobs/api/jobPosting/{id}</c>,
/// verified 2026-09-12). The description is flattened to plain text here, once, so no HTML from
/// the source is ever stored. The criteria are taken by position — the list is always in that
/// order — because their labels come back in whichever language LinkedIn picked for the request.
/// Pure function over untrusted HTML.
/// </summary>
public static partial class LinkedInJobPostingParser
{
    public static JobSourceDetail Parse(string html)
    {
        var description = DescriptionRegex().Match(html) is { Success: true } markup
            ? NullIfEmpty(ToPlainText(markup.Groups[1].Value))
            : null;

        var criteria = CriteriaRegex().Matches(html).Select(m => Clean(m.Groups[1].Value)).ToList();

        return new JobSourceDetail(
            description,
            Seniority: At(criteria, 0),
            EmploymentType: At(criteria, 1),
            JobFunction: At(criteria, 2),
            Industries: At(criteria, 3));
    }

    private static string? At(List<string> values, int index) => index < values.Count ? NullIfEmpty(values[index]) : null;

    private static string ToPlainText(string markup)
    {
        var withBreaks = BlockEndRegex().Replace(markup, "\n");
        var stripped = TagRegex().Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped);
        var lines = decoded.Split('\n').Select(l => InlineWhitespaceRegex().Replace(l, " ").Trim());
        return BlankRunRegex().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    private static string Clean(string fragment)
    {
        var stripped = TagRegex().Replace(fragment, " ");
        return InlineWhitespaceRegex().Replace(WebUtility.HtmlDecode(stripped), " ").Trim();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex(@"<div[^>]*show-more-less-html__markup[^>]*>(.*?)</div>\s*<", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex(@"<span[^>]*description__job-criteria-text[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CriteriaRegex();

    [GeneratedRegex(@"<br\s*/?>|</p>|</li>|</h[1-6]>|</div>|</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\r\f\v\u00A0]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRunRegex();
}
