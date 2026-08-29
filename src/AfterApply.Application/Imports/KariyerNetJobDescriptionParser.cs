using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// kariyer.net's job pages publish a meta description following a fixed Turkish template:
/// "Kariyer.net'teki {Company} firmasına ait {JobTitle} iş ilanını hemen inceleyin ve başvurun!"
/// (verified against a real listing 2026-08-29). Unlike the page &lt;title&gt;/og:title (just
/// "{Company} {JobTitle} İş İlanı - {date}", with no delimiter between company and title), this
/// description has an unambiguous split point ("firmasına ait"). Best-effort: if kariyer.net
/// changes this template, this returns nulls and the generic og:title fallback in
/// JobLinkPreviewService takes over. Pure function — no I/O.
/// </summary>
public static partial class KariyerNetJobDescriptionParser
{
    public static (string? Title, string? Company) Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return (null, null);
        }

        var match = DescriptionTemplateRegex().Match(description);
        if (!match.Success)
        {
            return (null, null);
        }

        var company = match.Groups["company"].Value.Trim();
        var title = match.Groups["title"].Value.Trim();

        return string.IsNullOrEmpty(company) || string.IsNullOrEmpty(title)
            ? (null, null)
            : (title, company);
    }

    [GeneratedRegex(@"Kariyer\.net'teki (?<company>.+?) firmasına ait (?<title>.+?) iş ilanını", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionTemplateRegex();
}
