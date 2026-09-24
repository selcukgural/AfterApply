namespace AfterApply.Infrastructure.Persistence;

/// <summary>
/// Builds (I)LIKE patterns out of search text. Parameterisation keeps the text out of the SQL, but
/// not out of the pattern: an unescaped "%" or "_" is still a wildcard, so a search for "_" matches
/// every row and "%%%%…" makes Postgres do pointless backtracking. Pair with
/// <see cref="EscapeCharacter"/> as ILike's third argument.
/// </summary>
public static class LikePattern
{
    public const string EscapeCharacter = @"\";

    /// <summary>"%text%" with every character of the text taken literally.</summary>
    public static string Contains(string text) => $"%{Escape(text)}%";

    /// <summary>"text%": starts with the text, taken literally.</summary>
    public static string StartsWith(string text) => $"{Escape(text)}%";

    /// <summary>A typed "%" or "_" is a character to find, not a wildcard.</summary>
    public static string Escape(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
