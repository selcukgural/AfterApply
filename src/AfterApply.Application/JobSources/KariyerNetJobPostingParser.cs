using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AfterApply.Application.JobSources.Contracts;

namespace AfterApply.Application.JobSources;

/// <summary>
/// Reads the description and what passes for criteria out of a kariyer.net posting page
/// (<c>/is-ilani/{slug}-{id}</c>, verified 2026-09-14): the
/// <c>data-test="qualifications-and-job-description"</c> block flattened to text, the
/// "Aday Kriterleri" list (experience → seniority), and the employment-type badge. The page
/// also carries <c>lastPublishDate="dd.MM.yyyy"</c> as an attribute, read here for the card that
/// only knew "3 gün". Pure function over untrusted HTML; no HTML is ever stored.
/// </summary>
public static partial class KariyerNetJobPostingParser
{
    private static readonly string[] EmploymentTypes = ["Tam zamanlı", "Yarı zamanlı", "Dönemsel / Proje bazlı", "Dönemsel", "Stajyer", "Serbest zamanlı"];

    public static JobSourceDetail Parse(string html)
    {
        var description = DescriptionRegex().Match(html) is { Success: true } markup
            ? NullIfEmpty(ToPlainText(markup.Groups[1].Value))
            : null;

        string? seniority = null;
        foreach (Match criterion in CriterionRegex().Matches(html))
        {
            // The label carries the site's own bullet glyph in front of it.
            if (Clean(criterion.Groups[1].Value).TrimStart('•', ' ').StartsWith("Tecrübe", StringComparison.OrdinalIgnoreCase))
            {
                seniority = NullIfEmpty(Clean(criterion.Groups[2].Value));
                break;
            }
        }

        var employmentType = EmploymentTypes.FirstOrDefault(t => html.Contains(">" + t + "<", StringComparison.Ordinal)
                                                                  || html.Contains("> " + t + "<", StringComparison.Ordinal));

        return new JobSourceDetail(description, seniority, employmentType, JobFunction: null, Industries: null);
    }

    /// <summary>The date the site printed as the posting's own; null when absent or unreadable.</summary>
    public static DateOnly? ParsePublishDate(string html) =>
        PublishDateRegex().Match(html) is { Success: true } m
        && DateOnly.TryParseExact(m.Groups[1].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

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

    [GeneratedRegex(@"<div[^>]*data-test=""qualifications-and-job-description""[^>]*>(.*?)</div>\s*(?:<!---->\s*)*<div[^>]*(?:aligment|alignment)-container",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex(@"data-test=""alignment-list-title""[^>]*>(.*?)</div>\s*<div[^>]*data-test=""alignment-list-value""[^>]*>(.*?)</div>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CriterionRegex();

    [GeneratedRegex(@"lastPublishDate=""(\d{2}\.\d{2}\.\d{4})""", RegexOptions.IgnoreCase)]
    private static partial Regex PublishDateRegex();

    [GeneratedRegex(@"<br\s*/?>|</p>|</li>|</h[1-6]>|</div>|</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t\r\f\v ]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRunRegex();
}
