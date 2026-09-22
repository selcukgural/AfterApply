using System.Net;
using System.Text.RegularExpressions;

namespace AfterApply.Application.Common;

/// <summary>
/// Turns a fragment of markup into readable plain text: block ends become newlines, tags go, HTML
/// entities are decoded, and runs of whitespace collapse. Used for the plain <c>Job.Description</c>
/// we store next to the HTML one — the ATS APIs hand back HTML, while the CV scan, the AI job-fit
/// scoring and the search index all want text.
///
/// Not a sanitizer and not a substitute for one. The HTML kept alongside is still untrusted and is
/// re-sanitized at render time (see web's DOMPurify usage).
/// </summary>
public static partial class HtmlToPlainText
{
    public static string? Convert(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var withBreaks = BlockEndRegex().Replace(markup, "\n");
        var stripped = TagRegex().Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped);
        var lines = decoded.Split('\n').Select(line => InlineWhitespaceRegex().Replace(line, " ").Trim());
        var text = BlankRunRegex().Replace(string.Join('\n', lines), "\n\n").Trim();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    [GeneratedRegex(@"</(p|div|li|ul|ol|h[1-6]|tr|section)\s*>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRunRegex();
}
