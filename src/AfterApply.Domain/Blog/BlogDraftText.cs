using System.Net;
using System.Text.RegularExpressions;

namespace AfterApply.Domain.Blog;

/// <summary>
/// Whether a draft has anything written in it. The rule behind "create" (2026-09-19): a post row
/// exists only once the author has typed at least one character somewhere — the title, the
/// summary or the body — so opening the editor and closing it again leaves nothing behind. The
/// body is HTML, so its tags are dropped and its entities decoded before looking: an empty
/// paragraph (<c>&lt;p&gt;&lt;/p&gt;</c>) or one holding only <c>&amp;nbsp;</c> is not text.
/// </summary>
public static partial class BlogDraftText
{
    public static bool HasAny(string? title, string? excerpt, string? contentHtml)
        => !string.IsNullOrWhiteSpace(title)
           || !string.IsNullOrWhiteSpace(excerpt)
           || !string.IsNullOrWhiteSpace(TextOf(contentHtml));

    /// <summary>The body's text with its markup removed — only good enough to tell "something"
    /// from "nothing"; not a renderer.</summary>
    public static string TextOf(string? contentHtml)
        => string.IsNullOrEmpty(contentHtml) ? string.Empty : WebUtility.HtmlDecode(Tags().Replace(contentHtml, " "));

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}
