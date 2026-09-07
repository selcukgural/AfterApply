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
/// early by pasting one of their own.</item>
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
        return $"[{entry.Category}] {summary}";
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
    private static string Fence(string message)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in message)
        {
            currentRun = character == '`' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }

    /// <summary>Metadata is ours, not the user's, but it still lands in a table cell — a stray
    /// pipe from a User-Agent would split the row.</summary>
    private static void AppendRow(StringBuilder builder, string key, string value) =>
        builder.Append("| ").Append(key).Append(" | ")
            .Append(value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "))
            .Append(" |\n");

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1).TrimEnd(), "…");
}
