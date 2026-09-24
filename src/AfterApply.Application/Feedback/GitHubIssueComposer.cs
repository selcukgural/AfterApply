using System.Globalization;
using System.Text;
using AfterApply.Domain.Feedback;

namespace AfterApply.Application.Feedback;

/// <summary>
/// Turns a stored feedback entry into the title/body/label of its mirrored GitHub issue.
///
/// Two rules shape everything here:
///
/// <list type="bullet">
/// <item><b>Redact.</b> The mirror leaves our database, so it carries no reply address and no
/// account email — only the entry id, which is the key to look the real row up with. Answering
/// someone means opening the database, which is exactly the friction we want in front of a
/// personal address.</item>
/// <item><b>Quote, never interpolate.</b> The message is user input rendered as Markdown by a
/// third party. Inside a fenced block it cannot become a heading, a link, an image, a
/// <c>#123</c> issue cross-reference, or an <c>@name</c> that notifies a stranger. The fence is
/// grown past the longest backtick run in the text, per CommonMark, so the user cannot close it
/// early by pasting one of their own. The title cannot be fenced, so a word joiner after every
/// "@" and "#" keeps GitHub from reading a mention or a cross-reference there, and the metadata
/// cells — client-supplied too, a page path or a User-Agent is whatever the request said — go
/// in code spans.</item>
/// </list>
/// </summary>
public static class GitHubIssueComposer
{
    private const int MaxTitleLength = 72;

    public static string Label(FeedbackCategory category) => category switch
    {
        FeedbackCategory.Bug => "feedback:bug",
        FeedbackCategory.Idea => "feedback:idea",
        FeedbackCategory.Question => "feedback:question",
        _ => "feedback"
    };

    public static string Title(FeedbackEntry entry)
    {
        // First line only: a title is one line whatever the user pressed Enter in.
        var firstLine = entry.Message.ReplaceLineEndings("\n").Split('\n')[0].Trim();
        var summary = firstLine.Length == 0 ? "(no summary)" : Truncate(firstLine, MaxTitleLength);
        return $"[{entry.Category}] {Defuse(summary)}";
    }

    public static string Body(FeedbackEntry entry)
    {
        var builder = new StringBuilder();

        builder.Append(Fence(entry.Message)).Append('\n');
        builder.Append(entry.Message.ReplaceLineEndings("\n")).Append('\n');
        builder.Append(Fence(entry.Message)).Append("\n\n");

        builder.Append("| | |\n|---|---|\n");
        AppendRow(builder, "Feedback id", entry.Id.ToString());
        AppendRow(builder, "Category", entry.Category.ToString());
        AppendRow(builder, "Mood", entry.Mood?.ToString() ?? "—");
        AppendRow(builder, "Page", entry.PagePath ?? "—");
        AppendRow(builder, "Locale", entry.Locale ?? "—");
        AppendRow(builder, "Theme", entry.Theme ?? "—");
        AppendRow(builder, "User agent", entry.UserAgent ?? "—");
        AppendRow(builder, "Submitted", entry.SubmittedAt.ToString("u", CultureInfo.InvariantCulture));
        AppendRow(builder, "Wants a reply", entry.ReplyEmail is null ? "no" : "yes — address is in the database");

        builder.Append("\nSent from the e-kariyerim in-app feedback panel. The sender's identity and reply\n");
        builder.Append("address are deliberately not mirrored here — look the feedback id up in FeedbackEntries.\n");

        return builder.ToString();
    }

    /// <summary>A fence at least three backticks long, and always longer than the longest run
    /// inside the text — otherwise a pasted ``` closes the block and the rest renders as Markdown.</summary>
    private static string Fence(string message) =>
        new('`', Math.Max(3, LongestBacktickRun(message) + 1));

    private static int LongestBacktickRun(string text)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in text)
        {
            currentRun = character == '`' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return longestRun;
    }

    /// <summary>The value goes in a code span, so a page path or User-Agent carrying a link, an
    /// image or an @name renders as text. GFM splits table cells before it parses code spans, so a
    /// pipe still needs its backslash inside one.</summary>
    private static void AppendRow(StringBuilder builder, string key, string value)
    {
        var flat = value.ReplaceLineEndings(" ");
        var fence = new string('`', LongestBacktickRun(flat) + 1);
        builder.Append("| ").Append(key).Append(" | ")
            .Append(fence).Append(' ')
            .Append(flat.Replace("|", "\\|", StringComparison.Ordinal))
            .Append(' ').Append(fence)
            .Append(" |\n");
    }

    /// <summary>A word joiner (U+2060) after "@" and "#": invisible, but it breaks the token GitHub
    /// looks for, so "@someone" notifies nobody and "#12" or "org/repo#12" links nothing.</summary>
    private static string Defuse(string text) =>
        text.Replace("@", "@\u2060", StringComparison.Ordinal).Replace("#", "#\u2060", StringComparison.Ordinal);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1).TrimEnd(), "…");
}
