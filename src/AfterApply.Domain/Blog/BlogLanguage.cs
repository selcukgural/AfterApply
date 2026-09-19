namespace AfterApply.Domain.Blog;

/// <summary>
/// The languages a post can be written in — the web app's two locales. One post carries one
/// language (DECISIONS.md 2026-09-19): <c>/tr/blog</c> lists Turkish posts and <c>/en/blog</c>
/// English ones, and a pair of posts can point at each other as translations. Strings rather than
/// an enum because the value is a URL segment and an <c>hreflang</c> code as it stands.
/// </summary>
public static class BlogLanguage
{
    public const string Tr = "tr";
    public const string En = "en";

    public const int MaxLength = 2;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Tr, En };

    public static bool IsSupported(string? language) => language is not null && All.Contains(language);
}
