using System.Text.RegularExpressions;

namespace AfterApply.Domain.Blog;

/// <summary>
/// What the generated cover of a blog post says and shows (DECISIONS.md 2026-09-27): a short
/// line of its own and one icon's name. The web app draws the cover from these whenever the author
/// has not uploaded an image — on the list card and as the share image — so a post always has
/// one. Both optional: no hook draws the title, no icon draws the default one.
///
/// The icon is a name from the icon set the web app ships (Lucide). The set lives there, not here,
/// so this checks only that the value is shaped like one of its names; the web app draws an
/// unknown name as the default icon. A name picks a drawing from a fixed table — it is never
/// markup, never a path, never a URL.
/// </summary>
public sealed partial record BlogCoverCard(string? Hook, string? Icon)
{
    /// <summary>The cover is a picture, not a paragraph: past this the line stops fitting the
    /// card at a readable size. The editor's counter shows the same number.</summary>
    public const int MaxHookLength = 60;

    public const int MaxIconLength = 64;

    public static readonly BlogCoverCard Empty = new(null, null);

    public bool IsEmpty => Hook is null && Icon is null;

    /// <summary>Trims, folds any run of whitespace (a newline included) into one space, turns ""
    /// into null and lower-cases the icon — the one place the form's raw values become what is
    /// stored.</summary>
    public static BlogCoverCard Normalize(string? hook, string? icon)
    {
        var line = hook is null ? null : WhitespaceRuns().Replace(hook, " ").Trim();
        var name = icon?.Trim().ToLowerInvariant();
        return new BlogCoverCard(string.IsNullOrEmpty(line) ? null : line, string.IsNullOrEmpty(name) ? null : name);
    }

    public bool IsValid => (Hook?.Length ?? 0) <= MaxHookLength && (Icon is null || IsIconName(Icon));

    /// <summary>Lower-case letters and digits in dash-separated words: the shape of every name in
    /// the icon set, and nothing that could mean anything else where it lands.</summary>
    public static bool IsIconName(string? value) =>
        value is { Length: > 0 and <= MaxIconLength } && IconName().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex IconName();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();
}
